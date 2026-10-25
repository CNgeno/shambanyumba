# Deploy-SmartEstate.ps1
# Publishes SmartBiz Estate locally, compares every published file with the server's copy (SHA-256),
# and uploads ONLY the files that changed - usually the RealEstateERP.*.dll files and the
# staticwebassets.endpoints.json. The server then backs up the files being replaced, swaps them in,
# restarts the service, checks /health, and rolls back automatically if the site doesn't come up.
#
# Run from the repo root:   .\Deploy-SmartEstate.ps1
# See what would change:    .\Deploy-SmartEstate.ps1 -DryRun
#
# REQUIRES: PuTTY's plink.exe and pscp.exe on PATH (or set $PlinkPath/$PscpPath below).

param(
    [string]$ServerIP = "178.162.244.255",
    [string]$ServerUser = "root",
    # Never write the password here (this repo is shared). Leave empty to be asked when the script runs.
    [string]$ServerPassword = $env:SMARTESTATE_SSH_PASSWORD,
    [string]$PlinkPath = "plink.exe",
    [string]$PscpPath  = "pscp.exe",
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
$ServerPath = "/home/builder/SmartEstate/publish"
$Project = "./src/RealEstateERP.Web"
$LocalPublish = "./publish-deploy"
$Changed = "./publish-changed"
$ArchiveFile = "./smartestate-changed.tar.gz"
$ApplyScript = "./deploy/smartestate-apply.sh"
$Stamp = Get-Date -Format "yyyyMMdd-HHmmss"

# Never sent: debug symbols, the Windows launcher, IIS-only config, and settings. appsettings.json holds the
# local dev connection string; the server keeps its own copy with the production database and email settings.
$Excluded = @("*.pdb", "*.exe", "web.config", "appsettings.json", "appsettings.Development.json")

# PuTTY's first-connect host-key prompt would hang a non-interactive script, so we auto-accept
# on first connect (TOFU). Remove -hostkey / -batch to be stricter.
$Interactive = [Environment]::UserInteractive -and -not [Console]::IsInputRedirected

# Fall back to the git-ignored .env next to this script (KEY=value lines).
$EnvFile = Join-Path $PSScriptRoot ".env"
if ([string]::IsNullOrEmpty($ServerPassword) -and (Test-Path $EnvFile)) {
    foreach ($line in Get-Content $EnvFile) {
        if ($line -match '^\s*SMARTESTATE_SSH_PASSWORD\s*=\s*(.*?)\s*$') { $ServerPassword = $Matches[1].Trim('"', "'") }
    }
}

if ([string]::IsNullOrEmpty($ServerPassword)) {
    if (-not $Interactive) {
        Write-Host "CRITICAL: No SSH password. Set `$env:SMARTESTATE_SSH_PASSWORD or pass -ServerPassword (no terminal to prompt in)." -ForegroundColor Red
        exit 1
    }
    $secure = Read-Host "SSH password for $ServerUser@$ServerIP" -AsSecureString
    $ServerPassword = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure))
}
$PlinkArgs = @("-batch", "-ssh", "-pw", $ServerPassword, "$ServerUser@$ServerIP")
$PscpArgs  = @("-batch", "-pw", $ServerPassword)

function Fail($message) {
    Write-Host "CRITICAL: $message" -ForegroundColor Red
    if ($Interactive) { Read-Host "Press Enter to exit" }
    exit 1
}


# 1. Publish locally ---------------------------------------------------------------------------
Write-Host "Publishing SmartBiz Estate (Release)..." -ForegroundColor Cyan
foreach ($p in @($LocalPublish, $Changed, $ArchiveFile)) { if (Test-Path $p) { Remove-Item -Recurse -Force $p } }

dotnet publish $Project -c Release -o $LocalPublish -p:UseAppHost=false
if ($LASTEXITCODE -ne 0) { Fail "Local publish failed." }

# 2. Compare with the server -------------------------------------------------------------------
Write-Host "Reading file fingerprints from the server..." -ForegroundColor Cyan
$remoteLines = & $PlinkPath @PlinkArgs "cd $ServerPath && find . -type f -print0 | xargs -0 sha256sum"
if ($LASTEXITCODE -ne 0) { Fail "Could not read the server's files over SSH." }

$remote = @{}
foreach ($line in $remoteLines) {
    if ($line -match '^([0-9a-f]{64})\s+\./(.+)$') { $remote[$Matches[2]] = $Matches[1] }
}

$root = (Resolve-Path $LocalPublish).Path.TrimEnd('\', '/')
$toUpload = @()
foreach ($file in Get-ChildItem $root -Recurse -File) {
    $skip = $false
    foreach ($pattern in $Excluded) { if ($file.Name -like $pattern) { $skip = $true; break } }
    if ($skip) { continue }

    $relative = $file.FullName.Substring($root.Length + 1).Replace('\', '/')
    $hash = (Get-FileHash $file.FullName -Algorithm SHA256).Hash.ToLower()
    if ($remote[$relative] -ne $hash) {
        $toUpload += [pscustomobject]@{ Path = $relative; Full = $file.FullName; New = -not $remote.ContainsKey($relative); Size = $file.Length }
    }
}

if ($toUpload.Count -eq 0) {
    Write-Host "`nThe server already has this exact build. Nothing to deploy." -ForegroundColor Green
    Remove-Item -Recurse -Force $LocalPublish
    exit 0
}

$totalKb = [math]::Round(($toUpload | Measure-Object Size -Sum).Sum / 1KB)
Write-Host "`n$($toUpload.Count) file(s) changed ($totalKb KB):" -ForegroundColor Yellow
foreach ($f in $toUpload) { Write-Host ("  {0} {1}" -f $(if ($f.New) { "+" } else { "~" }), $f.Path) }

if ($DryRun) {
    Write-Host "`nDry run: nothing was uploaded." -ForegroundColor Gray
    Remove-Item -Recurse -Force $LocalPublish
    exit 0
}

# 3. Package only the changed files ------------------------------------------------------------
foreach ($f in $toUpload) {
    $target = Join-Path $Changed $f.Path
    New-Item -ItemType Directory -Force -Path (Split-Path $target) | Out-Null
    Copy-Item $f.Full $target
}
tar -czf $ArchiveFile -C $Changed .
if ($LASTEXITCODE -ne 0) { Fail "Compression failed." }

# The server script must have Linux line endings, whatever git did to it on Windows.
$applyLocal = "./smartestate-apply-$Stamp.sh"
$applyText = (Get-Content $ApplyScript -Raw) -replace "`r", ""
[IO.File]::WriteAllText((Join-Path (Get-Location) $applyLocal), $applyText, (New-Object Text.UTF8Encoding $false))

# 4. Upload and apply --------------------------------------------------------------------------
$remoteArchive = "/tmp/smartestate-$Stamp.tar.gz"
$remoteApply = "/tmp/smartestate-apply-$Stamp.sh"

Write-Host "`nUploading changed files..." -ForegroundColor Cyan
& $PscpPath @PscpArgs $ArchiveFile "${ServerUser}@${ServerIP}:$remoteArchive"
if ($LASTEXITCODE -ne 0) { Fail "Upload failed." }
& $PscpPath @PscpArgs $applyLocal "${ServerUser}@${ServerIP}:$remoteApply"
if ($LASTEXITCODE -ne 0) { Fail "Upload failed." }

Write-Host "Applying on the server (backup, swap, restart, health check)..." -ForegroundColor Cyan
$output = & $PlinkPath @PlinkArgs "bash $remoteApply $remoteArchive; rc=`$?; rm -f $remoteApply $remoteArchive; exit `$rc"
$exitCode = $LASTEXITCODE
$output | ForEach-Object { Write-Host "  $_" -ForegroundColor Gray }

foreach ($p in @($LocalPublish, $Changed, $ArchiveFile, $applyLocal)) { if (Test-Path $p) { Remove-Item -Recurse -Force $p } }

if ($exitCode -eq 0 -and ($output -match "SUCCESS")) {
    Write-Host "`nDeployed successfully: https://shambanyumba.com" -ForegroundColor Green
} else {
    Fail "Deployment failed (see the server output above). If it says ROLLED BACK, the previous version is live again."
}