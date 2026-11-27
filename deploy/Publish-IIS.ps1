<#
.SYNOPSIS
    Builds the two IIS packages (Admin site and STS sign-in site) for one environment.

.DESCRIPTION
    Runs the unit tests, then 'dotnet publish' (Release, framework-dependent) for:
      * Clopay.IdentityServer4.Admin         -> artifacts\<Environment>\Admin
      * Clopay.IdentityServer4.STS.Identity  -> artifacts\<Environment>\STS
    and zips each one.

    The generated web.config sets ASPNETCORE_ENVIRONMENT to the chosen environment, so the matching
    appsettings.<Environment>.json is used. The package only contains the settings for THAT environment
    (other environments' files - which hold their SQL logins - and the Local file are removed), and no
    seed files with a default password (use -IncludeSeed only for the one-time bootstrap of a new admin database).

    The target server needs the ASP.NET Core 6 Hosting Bundle (runtime 6.0.x). See DEPLOY-IIS.md.

.EXAMPLE
    .\deploy\Publish-IIS.ps1 -Environment Test
    .\deploy\Publish-IIS.ps1 -Environment Production -SkipTests
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [ValidateSet('Development', 'Test', 'Staging', 'Production')] [string]$Environment,
    [string]$Output,
    [switch]$SkipTests,
    [switch]$IncludeSeed,
    [switch]$NoZip
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
if (-not $Output) { $Output = Join-Path $repo 'artifacts' }
$out = Join-Path $Output $Environment

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'The .NET SDK (6.0 or newer) is required to build. Install it and try again.' }

$apps = @(
    @{ Name = 'Admin'; Project = 'src\Clopay.IdentityServer4.Admin\Clopay.IdentityServer4.Admin.csproj' },
    @{ Name = 'STS';   Project = 'src\Clopay.IdentityServer4.STS.Identity\Clopay.IdentityServer4.STS.Identity.csproj' }
)

if (Test-Path $out) { Remove-Item $out -Recurse -Force }
New-Item -ItemType Directory -Force -Path $out | Out-Null

if (-not $SkipTests) {
    Write-Host 'Running unit tests ...'
    & dotnet test (Join-Path $repo 'tests\Clopay.IdentityServer4.Admin.UnitTests\Clopay.IdentityServer4.Admin.UnitTests.csproj') -c Release --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw 'Unit tests failed - nothing was published. (Use -SkipTests to override.)' }
}

$sha = ''
if (Get-Command git -ErrorAction SilentlyContinue) { $sha = (& git -C $repo rev-parse --short HEAD 2>$null) }
$otherEnvs = 'Local', 'Development', 'Test', 'Staging', 'Production' | Where-Object { $_ -ne $Environment }

foreach ($app in $apps) {
    $dest = Join-Path $out $app.Name
    Write-Host "Publishing $($app.Name) ($Environment) ..."
    $log = & dotnet publish (Join-Path $repo $app.Project) -c Release -o $dest --nologo -v q "-p:EnvironmentName=$Environment" 2>&1
    if ($LASTEXITCODE -ne 0) { $log | Select-Object -Last 40 | ForEach-Object { Write-Host $_ }; throw "dotnet publish failed for $($app.Name)." }

    # --- keep only this environment's settings; drop dev-only and build-only files
    foreach ($e in $otherEnvs) {
        Get-ChildItem $dest -File -Filter "*.$e.json" -ErrorAction SilentlyContinue | Remove-Item -Force
    }
    foreach ($junk in 'package.json', 'package-lock.json', 'gulpfile.js', 'tempkey.jwk') {
        $p = Join-Path $dest $junk
        if (Test-Path $p) { Remove-Item $p -Force }
    }
    if (-not $IncludeSeed) {
        Get-ChildItem $dest -File -Filter 'identitydata*.json' -ErrorAction SilentlyContinue | Remove-Item -Force
        Get-ChildItem $dest -File -Filter 'identityserverdata*.json' -ErrorAction SilentlyContinue | Remove-Item -Force
    }

    # --- sanity checks
    if (-not (Test-Path (Join-Path $dest 'web.config'))) { throw "$($app.Name): web.config was not generated." }
    $envFile = Join-Path $dest "appsettings.$Environment.json"
    if (-not (Test-Path $envFile)) { throw "$($app.Name): appsettings.$Environment.json is missing from the package." }
    if ((Get-Content (Join-Path $dest 'web.config') -Raw) -notmatch [regex]::Escape($Environment)) { Write-Warning "$($app.Name): web.config does not mention ASPNETCORE_ENVIRONMENT=$Environment - check it before deploying." }

    $left = @(@($envFile, (Join-Path $dest "serilog.$Environment.json")) | Where-Object { Test-Path $_ } | ForEach-Object { Select-String -Path $_ -Pattern '<[A-Z_]{3,}>' -AllMatches } | ForEach-Object { $_.Matches.Value } | Sort-Object -Unique)
    if ($left.Count -gt 0) { Write-Warning ("{0}: the {1} settings still have placeholders to supply at deploy time: {2}" -f $app.Name, $Environment, ($left -join ' ')) }

    "Clopay IAM Admin - $($app.Name)`r`nEnvironment : $Environment`r`nBuilt       : $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')`r`nCommit      : $sha`r`n" | Set-Content (Join-Path $dest 'package-info.txt') -Encoding ascii

    if (-not $NoZip) {
        $zip = Join-Path $out ("{0}-{1}.zip" -f $app.Name, $Environment)
        Compress-Archive -Path (Join-Path $dest '*') -DestinationPath $zip -Force
        Write-Host "  -> $zip"
    }
}

Write-Host ''
Write-Host "Packages are in $out" -ForegroundColor Green
Write-Host '  Admin : upload to the Admin site folder     STS : upload to the STS site folder'
Write-Host '  Deploy safely with:  .\deploy\Deploy-IIS.ps1 -PackagePath <package folder> -TargetPath <IIS site folder>'
