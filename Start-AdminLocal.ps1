<#
.SYNOPSIS
    Runs the Clopay IAM Admin UI on your machine against a Clopay IDP database - without ever
    migrating or seeding that database.

.DESCRIPTION
    Two apps start:
      * STS   https://localhost:44310  - the admin sign-in service
      * Admin https://localhost:44303  - the UI; its Clients / API resources / Scopes / Secrets /
                                         Persisted-grant pages read the REAL IDP database

    Two databases are involved:
      * IDP database   - the real Clopay IdentityServer database. Never migrated or seeded
                         (ApplyDatabaseMigrations = false, ApplySeed = false).
      * Admin database - the tool's own users, audit log and keys. By default this is a SQL Server
                         database called IdentityServerAdminDB on the SAME server and login as the IDP
                         database. Use -AdminConnectionString for a different one, or -UseLocalDb for a
                         throw-away LocalDB database instead (no shared server needed).

    The first time, the admin database is empty, so the script creates its tables and the 'admin' user
    (you are asked to confirm before anything is written to a shared server).

    The IDP connection string is read from src\Clopay.IdentityServer4.Admin\appsettings.<Environment>.json
    (ConfigurationDbConnection). To make the UI unable to change the IDP database while you evaluate it, pass a
    read-only SQL login with -ConfigConnectionString (see _designs\Create-ReadOnly-Login.sql).

    Sign in with the seeded user from src\Clopay.IdentityServer4.Admin\identitydata.json: 'admin' and the
    password in that file. On a shared server CHANGE THAT PASSWORD after the first sign-in.

.EXAMPLE
    .\Start-AdminLocal.ps1                                  # Test IDP database + IdentityServerAdminDB on the same server
    .\Start-AdminLocal.ps1 -Environment Development
    .\Start-AdminLocal.ps1 -ConfigConnectionString 'Data Source=...;Initial Catalog=IdentityServerDB_DBTest;User ID=idp_admin_ro;Password=...;TrustServerCertificate=True'
    .\Start-AdminLocal.ps1 -AdminConnectionString 'Data Source=...;Initial Catalog=MyAdminDb;User ID=...;Password=...'
    .\Start-AdminLocal.ps1 -UseLocalDb                      # admin database on LocalDB instead
    .\Start-AdminLocal.ps1 -Stop                            # stop both apps
    .\Start-AdminLocal.ps1 -Stop -UseLocalDb -Reset         # ...and drop the LocalDB database
#>
[CmdletBinding()]
param(
    [ValidateSet('Development', 'Test', 'Staging', 'Production')] [string]$Environment = 'Test',
    [string]$IdpAppSettings,
    [string]$ConfigConnectionString,
    [string]$AdminConnectionString,
    [switch]$UseLocalDb,
    [switch]$AllowProduction,
    [switch]$Yes,
    [switch]$Stop,
    [switch]$Reset
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Data
$repo    = $PSScriptRoot
$src     = Join-Path $repo 'src'
$run     = Join-Path $env:TEMP 'ClopayAdminLocal'
$localDb = 'ClopayAdminLocal'
$localCs = "Server=(localdb)\mssqllocaldb;Database=$localDb;Trusted_Connection=True;MultipleActiveResultSets=true"
$adminDbName = 'IdentityServerAdminDB'
$adminProj = 'Clopay.IdentityServer4.Admin'
$stsProj   = 'Clopay.IdentityServer4.STS.Identity'
New-Item -ItemType Directory -Force -Path $run | Out-Null

function Stop-Apps {
    Get-ChildItem $run -Filter *.pid -ErrorAction SilentlyContinue | ForEach-Object {
        try { Stop-Process -Id ([int](Get-Content $_.FullName)) -Force -ErrorAction Stop } catch {}
        Remove-Item $_.FullName -Force
    }
}
function Invoke-SqlScalar($cs, $sql) {
    $c = New-Object System.Data.SqlClient.SqlConnection $cs
    try { $c.Open(); $cmd = $c.CreateCommand(); $cmd.CommandText = $sql; $cmd.CommandTimeout = 30; return $cmd.ExecuteScalar() } finally { $c.Dispose() }
}
# The admin database is "ready" once the Admin app has created its tables and seeded the admin user.
function Test-AdminDbReady($cs) { try { return ([int](Invoke-SqlScalar $cs 'SELECT COUNT(*) FROM dbo.Users') -ge 1) } catch { return $false } }
function Get-Part($cs, $pattern) { return [regex]::Match($cs, $pattern, 'IgnoreCase').Groups[1].Value }
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
function Set-AdminConnections($cs) { foreach ($n in 'ConfigurationDbConnection', 'PersistedGrantDbConnection', 'IdentityDbConnection', 'AdminLogDbConnection', 'AdminAuditLogDbConnection', 'DataProtectionDbConnection') { Set-Item "Env:ConnectionStrings__$n" $cs } }

if ($Stop) {
    Stop-Apps
    if ($Reset) {
        if ($UseLocalDb) {
            if (Test-AdminDbReady $localCs) { & sqlcmd -S '(localdb)\mssqllocaldb' -E -Q "ALTER DATABASE [$localDb] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$localDb];" | Out-Null; Write-Host "Dropped local database $localDb." }
        } else {
            Write-Host "-Reset only drops the LocalDB database (-UseLocalDb). A SQL Server admin database is never dropped by this script - ask your DBA." -ForegroundColor Yellow
        }
    }
    Write-Host 'Stopped.'; return
}
if ($Environment -eq 'Production' -and -not $AllowProduction) { throw 'Refusing to point the Admin UI at Production without -AllowProduction (and a read-only login).' }

# --- where is the IDP database?
if (-not $IdpAppSettings) { $IdpAppSettings = Join-Path $src "$adminProj\appsettings.$Environment.json" }
$fileCs = $null
if (Test-Path $IdpAppSettings) { $fileCs = [regex]::Match((Get-Content $IdpAppSettings -Raw), '"(?:ConfigurationDbConnection|IdentityServerDB)"\s*:\s*"([^"]+)"').Groups[1].Value }
if ($fileCs -match '<[A-Z_]+>') { Write-Warning "$IdpAppSettings still contains placeholders ($($Matches[0])) - pass -ConfigConnectionString and -AdminConnectionString instead."; $fileCs = $null }
if (-not $ConfigConnectionString) {
    if (-not $fileCs) { throw "No usable ConfigurationDbConnection found in $IdpAppSettings (pass -IdpAppSettings or -ConfigConnectionString)" }
    $ConfigConnectionString = $fileCs
}
Write-Host ("IDP database   : {0} / {1}  (as {2})" -f (Get-Part $ConfigConnectionString '(?:Data Source|Server)=([^;]+)'), (Get-Part $ConfigConnectionString '(?:Initial Catalog|Database)=([^;]+)'), (Get-Part $ConfigConnectionString 'User ID=([^;]+)'))

# --- where is the admin database?
if ($UseLocalDb) {
    $adminCs = $localCs; $adminLabel = "LocalDB / $localDb"; $shared = $false
} elseif ($AdminConnectionString) {
    $adminCs = $AdminConnectionString; $adminLabel = ("{0} / {1}" -f (Get-Part $adminCs '(?:Data Source|Server)=([^;]+)'), (Get-Part $adminCs '(?:Initial Catalog|Database)=([^;]+)')); $shared = $true
} else {
    # Same server and login as the IDP database (from the Admin appsettings for this environment), but the separate admin database.
    # Deliberately NOT derived from -ConfigConnectionString: that may be a read-only login, and the admin database needs to be writable.
    if (-not $fileCs) { throw 'Cannot work out the admin database. Pass -AdminConnectionString, or use -UseLocalDb.' }
    $adminCs = [regex]::Replace($fileCs, '(Initial Catalog|Database)=[^;]+', "`$1=$adminDbName", 'IgnoreCase')
    $adminLabel = ("{0} / {1}  (as {2})" -f (Get-Part $adminCs '(?:Data Source|Server)=([^;]+)'), $adminDbName, (Get-Part $adminCs 'User ID=([^;]+)')); $shared = $true
}
Write-Host "Admin database : $adminLabel"

Stop-Apps
foreach ($proj in $adminProj, $stsProj) {
    # Always build (incremental, fast when nothing changed) - a stale bin\Release silently serves old views and old CSS.
    Write-Host "Building $proj ..."
    $out = & dotnet build (Join-Path $src "$proj\$proj.csproj") -c Release -v q -nologo 2>&1
    if ($LASTEXITCODE -ne 0) { $out | Select-Object -Last 40 | ForEach-Object { Write-Host $_ }; throw "Build failed for $proj - fix the errors above." }
}
$env:ASPNETCORE_ENVIRONMENT = 'Local'   # appsettings.Local.json in both apps (-Environment only picks which IDP database to read)

# --- first run: create + seed ONLY the admin database (the Admin app owns migrations/seed for all contexts)
if (-not (Test-AdminDbReady $adminCs)) {
    if ($shared -and -not $Yes) {
        Write-Host ''
        Write-Host "The admin database is empty or missing. First run will create its tables and an 'admin' user in:" -ForegroundColor Yellow
        Write-Host "    $adminLabel" -ForegroundColor Yellow
        Write-Host '    (nothing is written to the IDP database; if the database does not exist the login needs permission to create it)'
        if ((Read-Host 'Continue? (y/N)') -notmatch '^(y|yes)$') { Write-Host 'Cancelled - nothing was changed.'; return }
    }
    Write-Host 'First run: creating the admin database ...'
    Set-AdminConnections $adminCs
    $env:DatabaseMigrationsConfiguration__ApplyDatabaseMigrations = 'true'; $env:SeedConfiguration__ApplySeed = 'true'
    Start-App 'bootstrap' $adminProj 'https://localhost:44303'
    # The bootstrap Admin returns 500 on '/' until the STS exists, so wait for the seed data instead of HTTP.
    $seeded = $false
    for ($i = 0; $i -lt 150 -and -not $seeded; $i++) {
        Start-Sleep -Seconds 2
        try { $n = [int](Invoke-SqlScalar $adminCs 'SELECT (SELECT COUNT(*) FROM dbo.Users) + (SELECT COUNT(*) FROM dbo.Clients)'); if ($n -ge 2) { $seeded = $true } } catch {}
    }
    if (-not $seeded) { Stop-Apps; throw "The admin database was not seeded - see $run\bootstrap.out.log and $run\bootstrap.err.log" }
    Start-Sleep -Seconds 3; Stop-Apps
}

# --- normal run
$env:DatabaseMigrationsConfiguration__ApplyDatabaseMigrations = 'false'; $env:SeedConfiguration__ApplySeed = 'false'
$env:AdminConfiguration__IdentityServerBaseUrl    = 'https://localhost:44310'
$env:AdminConfiguration__IdentityAdminRedirectUri = 'https://localhost:44303/signin-oidc'
$env:AdminConfiguration__IdentityAdminBaseUrl     = 'https://localhost:44303'
$env:AdminConfiguration__ClientSecret = [regex]::Match((Get-Content (Join-Path $src "$adminProj\appsettings.Local.json") -Raw), '"ClientSecret"\s*:\s*"([^"]+)"').Groups[1].Value
Set-AdminConnections $adminCs
Start-App 'sts' $stsProj 'https://localhost:44310'
if (-not (Wait-Http 'https://localhost:44310/.well-known/openid-configuration' 120)) { throw "STS did not start - see $run\sts.out.log and $run\sts.err.log" }
Set-Item Env:ConnectionStrings__ConfigurationDbConnection  $ConfigConnectionString
Set-Item Env:ConnectionStrings__PersistedGrantDbConnection $ConfigConnectionString
Start-App 'admin' $adminProj 'https://localhost:44303'
if (-not (Wait-Http 'https://localhost:44303/' 120)) { throw "Admin did not start - see $run\admin.out.log and $run\admin.err.log" }

Write-Host ''
Write-Host 'Admin UI : https://localhost:44303   (sign in with the seeded user in identitydata.json)'
Write-Host "Logs     : $run"
Write-Host 'Stop     : .\Start-AdminLocal.ps1 -Stop'
