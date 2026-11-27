<#
.SYNOPSIS
    Copies a published package into an IIS site folder - safely, in place.

.DESCRIPTION
    Run this ON THE SERVER (or against a share) with a package folder made by Publish-IIS.ps1, e.g.
    artifacts\Test\Admin. It:
      1. checks the package (web.config, the app DLL, this environment's settings, leftover <PLACEHOLDERS>);
      2. BACKS UP the current contents of the target folder to a zip (skip with -NoBackup);
      3. takes the site offline with app_offline.htm so IIS releases the files;
      4. copies the new files over the old ones (robocopy). Files in the target that are not in the package are
         KEPT unless you pass -Clean, which mirrors the package exactly. Folders listed in -Preserve (default:
         Log, logs, App_Data) are never deleted;
      5. removes app_offline.htm so the site starts, and optionally checks a URL (-CheckUrl).

    It never touches IIS settings, app pools or bindings, and never touches a database.

    !! Use a separate folder (and IIS site) for the Admin and for the STS. Do NOT point this at the folder of the
    !! Clopay IDP or any other app that is still in use - -Clean would delete that app's files.

.EXAMPLE
    .\Deploy-IIS.ps1 -PackagePath .\artifacts\Test\Admin -TargetPath D:\Sites\iamui -WhatIf      # preview only
    .\Deploy-IIS.ps1 -PackagePath .\artifacts\Test\Admin -TargetPath D:\Sites\iamui -Clean -CheckUrl https://test-iamui.clopay.com/
    .\Deploy-IIS.ps1 -PackagePath .\artifacts\Test\STS   -TargetPath D:\Sites\iamui-sts
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)] [string]$PackagePath,
    [Parameter(Mandatory)] [string]$TargetPath,
    [string]$BackupPath,
    [string[]]$Preserve = @('Log', 'logs', 'App_Data'),
    [switch]$Clean,
    [switch]$NoBackup,
    [switch]$AllowPlaceholders,
    [switch]$Yes,
    [string]$CheckUrl
)
$ErrorActionPreference = 'Stop'

$PackagePath = (Resolve-Path $PackagePath).Path
if (-not (Test-Path (Join-Path $PackagePath 'web.config'))) { throw "No web.config in $PackagePath - point -PackagePath at a folder produced by Publish-IIS.ps1 (artifacts\<Env>\Admin or \STS)." }
$dll = Get-ChildItem $PackagePath -Filter 'Clopay.IdentityServer4.*.dll' | Where-Object { $_.Name -match '^Clopay\.IdentityServer4\.(Admin|STS\.Identity)\.dll$' } | Select-Object -First 1
if (-not $dll) { throw "$PackagePath does not contain the Admin or STS application DLL." }
$info = Join-Path $PackagePath 'package-info.txt'
$envName = if (Test-Path $info) { ((Get-Content $info | Where-Object { $_ -like 'Environment*' }) -replace '^Environment\s*:\s*', '').Trim() } else { '' }

# --- placeholders left in the settings?
if ($envName) {
    $files = @("appsettings.$envName.json", "serilog.$envName.json") | ForEach-Object { Join-Path $PackagePath $_ } | Where-Object { Test-Path $_ }
    $left = @($files | ForEach-Object { Select-String -Path $_ -Pattern '<[A-Z_]{3,}>' -AllMatches } | ForEach-Object { $_.Matches.Value } | Sort-Object -Unique)
    if ($left.Count -gt 0 -and -not $AllowPlaceholders) {
        throw ("The $envName settings still contain placeholders: {0}`nFill them in, or supply them as environment variables on the IIS app pool (they survive re-deploys) and re-run with -AllowPlaceholders." -f ($left -join ' '))
    }
}

$TargetPath = [System.IO.Path]::GetFullPath($TargetPath)
$exists = Test-Path $TargetPath
$existing = 0
if ($exists) { $existing = (Get-ChildItem $TargetPath -Recurse -File -ErrorAction SilentlyContinue | Measure-Object).Count }
if (-not $BackupPath) { $BackupPath = Join-Path (Split-Path -Parent $TargetPath) '_backups' }

Write-Host ''
Write-Host "Package   : $PackagePath   ($($dll.Name)$(if ($envName) { ", environment $envName" }))"
Write-Host "Target    : $TargetPath   ($(if ($exists) { "$existing existing files" } else { 'new folder' }))"
Write-Host "Mode      : $(if ($Clean) { 'CLEAN - files not in the package are deleted (except: ' + ($Preserve -join ', ') + ')' } else { 'overlay - existing files that are not in the package are kept' })"
Write-Host "Backup    : $(if ($NoBackup -or -not $exists) { 'none' } else { $BackupPath })"
Write-Host ''

# --- preview
if ($WhatIfPreference) {
    $rc = @($PackagePath, $TargetPath, '/L', '/NJH', '/NJS', '/NP')
    if ($Clean) { $rc += @('/MIR', '/XD') + $Preserve + @('/XF', 'app_offline.htm') } else { $rc += '/E' }
    Write-Host 'Preview (nothing is changed):'
    & robocopy @rc
    return
}

if (-not $Yes) {
    if ((Read-Host 'Deploy now? (y/N)') -notmatch '^(y|yes)$') { Write-Host 'Cancelled - nothing was changed.'; return }
}

# --- 1. backup
if ($exists -and $existing -gt 0 -and -not $NoBackup) {
    New-Item -ItemType Directory -Force -Path $BackupPath | Out-Null
    $zip = Join-Path $BackupPath ("{0}_{1}.zip" -f (Split-Path -Leaf $TargetPath), (Get-Date -Format 'yyyyMMdd_HHmmss'))
    Write-Host "Backing up current files to $zip ..."
    Compress-Archive -Path (Join-Path $TargetPath '*') -DestinationPath $zip -Force
    if (-not (Test-Path $zip)) { throw 'Backup failed - nothing was changed.' }
}
if (-not $exists) { New-Item -ItemType Directory -Force -Path $TargetPath | Out-Null }

# --- 2. offline
$offline = Join-Path $TargetPath 'app_offline.htm'
'<html><body style="font-family:Segoe UI,Arial;margin:3em"><h2>Clopay IAM Admin is being updated</h2><p>Back in a minute.</p></body></html>' | Set-Content $offline -Encoding utf8
Start-Sleep -Seconds 3

try {
    # --- 3. copy
    Write-Host 'Copying files ...'
    $rc = @($PackagePath, $TargetPath, '/R:3', '/W:2', '/NFL', '/NDL', '/NJH', '/NP')
    if ($Clean) { $rc += @('/MIR', '/XD') + $Preserve + @('/XF', 'app_offline.htm') } else { $rc += '/E' }
    & robocopy @rc | Out-Host
    if ($LASTEXITCODE -ge 8) { throw "robocopy failed (exit code $LASTEXITCODE). The site is still offline; the backup is in $BackupPath." }
}
finally {
    # --- 4. back online (only if the copy succeeded)
    if ($LASTEXITCODE -lt 8 -and (Test-Path $offline)) { Remove-Item $offline -Force }
}

Write-Host 'Deployed. The site restarts on the next request.' -ForegroundColor Green

# --- 5. optional check
if ($CheckUrl) {
    Start-Sleep -Seconds 3
    $code = & curl.exe -sk -o NUL -w '%{http_code}' --max-time 60 $CheckUrl
    if ($code -match '^(200|302)$') { Write-Host "Check: $CheckUrl -> HTTP $code" -ForegroundColor Green }
    else { Write-Warning "Check: $CheckUrl -> HTTP $code. Look at the site's Log folder and the Windows Event Log (source: IIS AspNetCore Module). To roll back, restore the zip from $BackupPath." }
}
