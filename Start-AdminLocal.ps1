<#
.SYNOPSIS
    Runs the Skoruba IdentityServer4 Admin UI locally against a Clopay IDP database - without ever
    migrating or seeding that database.

.DESCRIPTION
    Two apps start:
      * STS   https://localhost:44310  - the admin-login service (its own users/clients)
      * Admin https://localhost:44303  - the UI; its Clients / API resources / Scopes / Secrets /
                                         Persisted-grant pages read the REAL IDP database
    The Admin's own bookkeeping (admin users, audit/log tables, data-protection keys) and the STS data
    live in a throwaway LocalDB database (ClopayAdminLocal), created on first run. The IDP database is
    never migrated or seeded (ApplyDatabaseMigrations = false, ApplySeed = false).

    The IDP connection string is read from the IDP's own appsettings.<Environment>.json, so no secret is
    ever copied into this repo. To make the UI unable to write while you evaluate it, pass a read-only
    SQL login with -ConfigConnectionString (see _designs\Create-ReadOnly-Login.sql).

    Sign in with the local seed user from src\Skoruba.IdentityServer4.Admin\identitydata.json (local only).

.EXAMPLE
    .\Start-AdminLocal.ps1                                   # Test IDP database
    .\Start-AdminLocal.ps1 -ConfigConnectionString 'Data Source=...;Initial Catalog=IdentityServerDB_DBTest;User ID=idp_admin_ro;Password=...;TrustServerCertificate=True'
    .\Start-AdminLocal.ps1 -Stop                             # stop both apps
    .\Start-AdminLocal.ps1 -Stop -Reset                      # ...and drop the local LocalDB database
#>
[CmdletBinding()]
param(
    [ValidateSet('Development', 'Test', 'Staging', 'Production')] [string]$Environment = 'Test',
    [string]$IdpAppSettings,
    [string]$ConfigConnectionString,
    [switch]$AllowProduction,
    [switch]$Stop,
    [switch]$Reset
)
$ErrorActionPreference = 'Stop'
$repo   = $PSScriptRoot
$src    = Join-Path $repo 'src'
$run    = Join-Path $env:TEMP 'ClopayAdminLocal'
$dbName = 'ClopayAdminLocal'
$localCs = "Server=(localdb)\mssqllocaldb;Database=$dbName;Trusted_Connection=True;MultipleActiveResultSets=true"
New-Item -ItemType Directory -Force -Path $run | Out-Null

function Stop-Apps {
    Get-ChildItem $run -Filter *.pid -ErrorAction SilentlyContinue | ForEach-Object {
        try { Stop-Process -Id ([int](Get-Content $_.FullName)) -Force -ErrorAction Stop } catch {}
        Remove-Item $_.FullName -Force
    }
}
function Test-LocalDb { $r = & sqlcmd -S '(localdb)\mssqllocaldb' -E -h -1 -W -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.databases WHERE name = '$dbName'" 2>$null; return ("$r".Trim() -eq '1') }
function Start-App($name, $proj, $url) {
    $p = Start-Process -FilePath 'dotnet' -ArgumentList @('run', '--no-build', '-c', 'Release', '--no-launch-profile', '--urls', $url) `
        -WorkingDirectory (Join-Path $src $proj) -PassThru -WindowStyle Hidden `
        -RedirectStandardOutput (Join-Path $run "$name.out.log") -RedirectStandardError (Join-Path $run "$name.err.log")
    $p.Id | Out-File (Join-Path $run "$name.pid") -Encoding ascii
}
function Wait-Http($url, $secs) {
    for ($i = 0; $i -lt ($secs / 2); $i++) { Start-Sleep -Seconds 2; $c = & curl.exe -sk -o NUL -w '%{http_code}' --max-time 8 $url; if ($c -match '^(200|302)$') { return $true } }
    return $false
}
function Set-LocalConnections { foreach ($n in 'ConfigurationDbConnection', 'PersistedGrantDbConnection', 'IdentityDbConnection', 'AdminLogDbConnection', 'AdminAuditLogDbConnection', 'DataProtectionDbConnection') { Set-Item "Env:ConnectionStrings__$n" $localCs } }

if ($Stop) {
    Stop-Apps
    if ($Reset -and (Test-LocalDb)) { & sqlcmd -S '(localdb)\mssqllocaldb' -E -Q "ALTER DATABASE [$dbName] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$dbName];" | Out-Null; Write-Host "Dropped local database $dbName." }
    Write-Host 'Stopped.'; return
}
if ($Environment -eq 'Production' -and -not $AllowProduction) { throw 'Refusing to point the Admin UI at Production without -AllowProduction (and a read-only login).' }

# --- where is the IDP database?
if (-not $ConfigConnectionString) {
    if (-not $IdpAppSettings) { $IdpAppSettings = Join-Path $repo "cAppSettings\appsettings.$Environment.json" }
    if (-not (Test-Path $IdpAppSettings)) { throw "IDP appsettings not found: $IdpAppSettings (pass -IdpAppSettings or -ConfigConnectionString)" }
    $ConfigConnectionString = [regex]::Match((Get-Content $IdpAppSettings -Raw), '"IdentityServerDB"\s*:\s*"([^"]+)"').Groups[1].Value
    if (-not $ConfigConnectionString) { throw "No IdentityServerDB connection string in $IdpAppSettings" }
}
Write-Host ("IDP database : {0} / {1}  (as {2})" -f ([regex]::Match($ConfigConnectionString, '(?:Data Source|Server)=([^;]+)').Groups[1].Value), ([regex]::Match($ConfigConnectionString, '(?:Initial Catalog|Database)=([^;]+)').Groups[1].Value), ([regex]::Match($ConfigConnectionString, 'User ID=([^;]+)').Groups[1].Value))

Stop-Apps
foreach ($proj in 'Skoruba.IdentityServer4.Admin', 'Skoruba.IdentityServer4.STS.Identity') {
    # Always build (incremental, fast when nothing changed) - a stale bin\Release silently serves old views and old CSS.
    Write-Host "Building $proj ..."
    $out = & dotnet build (Join-Path $src "$proj\$proj.csproj") -c Release -v q -nologo 2>&1
    if ($LASTEXITCODE -ne 0) { $out | Select-Object -Last 40 | ForEach-Object { Write-Host $_ }; throw "Build failed for $proj - fix the errors above." }
}
$env:ASPNETCORE_ENVIRONMENT = 'Local'   # appsettings.Local.json in both apps (the -Environment parameter only picks which IDP database to read)

# --- first run: create + seed ONLY the local database (the Admin app owns migrations/seed for all contexts)
if (-not (Test-LocalDb)) {
    Write-Host 'First run: creating the local admin database ...'
    Set-LocalConnections
    $env:DatabaseMigrationsConfiguration__ApplyDatabaseMigrations = 'true'; $env:SeedConfiguration__ApplySeed = 'true'
    Start-App 'bootstrap' 'Skoruba.IdentityServer4.Admin' 'https://localhost:44303'
    # The bootstrap Admin returns 500 on '/' until the STS exists, so wait for the seed data instead of HTTP.
    $seeded = $false
    for ($i = 0; $i -lt 120 -and -not $seeded; $i++) {
        Start-Sleep -Seconds 2
        $r = & sqlcmd -S '(localdb)\mssqllocaldb' -E -h -1 -W -Q "SET NOCOUNT ON; SELECT (SELECT COUNT(*) FROM [$dbName].dbo.Users) + (SELECT COUNT(*) FROM [$dbName].dbo.Clients)" 2>$null
        if ("$r".Trim() -match '^\d+$' -and [int]"$r".Trim() -ge 2) { $seeded = $true }
    }
    if (-not $seeded) { Stop-Apps; throw "Local database was not seeded - see $run\bootstrap.out.log" }
    Start-Sleep -Seconds 3; Stop-Apps
}

# --- normal run
$env:DatabaseMigrationsConfiguration__ApplyDatabaseMigrations = 'false'; $env:SeedConfiguration__ApplySeed = 'false'
$env:AdminConfiguration__IdentityServerBaseUrl    = 'https://localhost:44310'
$env:AdminConfiguration__IdentityAdminRedirectUri = 'https://localhost:44303/signin-oidc'
$env:AdminConfiguration__IdentityAdminBaseUrl     = 'https://localhost:44303'
$env:AdminConfiguration__ClientSecret = [regex]::Match((Get-Content (Join-Path $src 'Skoruba.IdentityServer4.Admin\appsettings.Local.json') -Raw), '"ClientSecret"\s*:\s*"([^"]+)"').Groups[1].Value
Set-LocalConnections
Start-App 'sts' 'Skoruba.IdentityServer4.STS.Identity' 'https://localhost:44310'
if (-not (Wait-Http 'https://localhost:44310/.well-known/openid-configuration' 120)) { throw "STS did not start - see $run\sts.out.log" }
Set-Item Env:ConnectionStrings__ConfigurationDbConnection  $ConfigConnectionString
Set-Item Env:ConnectionStrings__PersistedGrantDbConnection $ConfigConnectionString
Start-App 'admin' 'Skoruba.IdentityServer4.Admin' 'https://localhost:44303'
if (-not (Wait-Http 'https://localhost:44303/' 120)) { throw "Admin did not start - see $run\admin.out.log" }

Write-Host ''
Write-Host 'Admin UI : https://localhost:44303   (sign in with the local seed user in identitydata.json)'
Write-Host "Logs     : $run"
Write-Host 'Stop     : .\Start-AdminLocal.ps1 -Stop'
