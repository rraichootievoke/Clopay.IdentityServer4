<#
.SYNOPSIS
    Builds the IIS package of the IDP Admin UI for one environment.

.DESCRIPTION
    Runs the unit tests, then `dotnet publish` (Release, framework-dependent, .NET 8) into
        artifacts\<Environment>\IDPAdminUI
    and zips it. The generated web.config sets ASPNETCORE_ENVIRONMENT to the chosen environment, so the matching
    appsettings.<Environment>.json is used. The package contains ONLY that environment's settings (other environments'
    files - which hold their SQL logins - and the Local file are removed).

    The server needs the ASP.NET Core 8 Hosting Bundle. See DEPLOY-IIS.md.

.EXAMPLE
    .\deploy\Publish-IIS.ps1 -Environment Staging
    .\deploy\Publish-IIS.ps1 -Environment Production -SkipTests
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [ValidateSet('Development', 'Test', 'Staging', 'Production')] [string]$Environment,
    [string]$Output,
    [switch]$SkipTests,
    [switch]$NoZip
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot          # the IDPAdminUI folder
if (-not $Output) { $Output = Join-Path $root 'artifacts' }
$out  = Join-Path $Output $Environment
$dest = Join-Path $out 'IDPAdminUI'
$project = Join-Path $root 'src\Clopay.IDPAdminUI\Clopay.IDPAdminUI.csproj'
$tests   = Join-Path $root 'tests\Clopay.IDPAdminUI.UnitTests\Clopay.IDPAdminUI.UnitTests.csproj'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'The .NET SDK (8.0 or newer) is required to build. Install it and try again.' }

if (Test-Path $out) { Remove-Item $out -Recurse -Force }
New-Item -ItemType Directory -Force -Path $out | Out-Null

if (-not $SkipTests) {
    Write-Host 'Running unit tests ...'
    & dotnet test $tests -c Release --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw 'Unit tests failed - nothing was published. (Use -SkipTests to override.)' }
}

Write-Host "Publishing IDPAdminUI ($Environment) ..."
$log = & dotnet publish $project -c Release -o $dest --nologo -v q "-p:EnvironmentName=$Environment" 2>&1
if ($LASTEXITCODE -ne 0) { $log | Select-Object -Last 40 | ForEach-Object { Write-Host $_ }; throw 'dotnet publish failed.' }

# --- keep only this environment's settings; drop build-only files
foreach ($e in ('Local', 'Development', 'Test', 'Staging', 'Production') | Where-Object { $_ -ne $Environment }) {
    Get-ChildItem $dest -File -Filter "*.$e.json" -ErrorAction SilentlyContinue | Remove-Item -Force
}
foreach ($junk in 'package.json', 'package-lock.json', 'gulpfile.js') {
    $p = Join-Path $dest $junk
    if (Test-Path $p) { Remove-Item $p -Force }
}
# folders the app writes to - present in the package so permissions can be set once and survive re-deploys
foreach ($dir in 'Log', 'App_Data') { New-Item -ItemType Directory -Force -Path (Join-Path $dest $dir) | Out-Null }

# --- sanity checks
if (-not (Test-Path (Join-Path $dest 'web.config'))) { throw 'web.config was not generated.' }
$envFile = Join-Path $dest "appsettings.$Environment.json"
if (-not (Test-Path $envFile)) { throw "appsettings.$Environment.json is missing from the package." }
if ((Get-Content (Join-Path $dest 'web.config') -Raw) -notmatch [regex]::Escape($Environment)) { Write-Warning "web.config does not mention ASPNETCORE_ENVIRONMENT=$Environment - check it before deploying." }

$left = @(Select-String -Path $envFile -Pattern '<[A-Za-z0-9_]{3,}>' -AllMatches | ForEach-Object { $_.Matches.Value } | Sort-Object -Unique)
if ($left.Count -gt 0) { Write-Warning ("appsettings.{0}.json still has placeholders to supply at deploy time: {1}" -f $Environment, ($left -join ' ')) }

$sha = ''
if (Get-Command git -ErrorAction SilentlyContinue) { $sha = (& git -C $root rev-parse --short HEAD 2>$null) }
@('Clopay IDP Admin UI', "Environment : $Environment", "Built       : $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')", "Commit      : $sha") | Set-Content (Join-Path $dest 'package-info.txt') -Encoding ascii

if (-not $NoZip) {
    $zip = Join-Path $out ("IDPAdminUI-{0}.zip" -f $Environment)
    Compress-Archive -Path (Join-Path $dest '*') -DestinationPath $zip -Force
    Write-Host "  -> $zip"
}

Write-Host ''
Write-Host "Package: $dest" -ForegroundColor Green
Write-Host '  Deploy it with:  .\deploy\Deploy-IIS.ps1 -PackagePath <that folder> -TargetPath <IIS site folder>'
