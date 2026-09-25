# ===================================================================
# Create Release Package for Internet Health Monitor
# ===================================================================
# This script creates a ZIP file ready for GitHub release
# It automatically reads the version from InternetHealth.ps1
# ===================================================================

# Extract version from InternetHealth.ps1
$scriptContent = Get-Content "InternetHealth.ps1" -Raw
if ($scriptContent -match '# Version: ([\d\.]+)') {
  $version = $matches[1]
}
else {
  Write-Host "ERROR: Could not find version in InternetHealth.ps1" -ForegroundColor Red
  Write-Host "Make sure the file contains '# Version: X.Y.Z'" -ForegroundColor Yellow
  exit 1
}

$outputName = "InternetHealthMonitor-v$version.zip"
$tempFolder = "release-temp"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Creating Release Package v$version" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Files to include in the release
$filesToInclude = @(
  "InternetHealth.ps1",
  "RUN_ME.bat",
  "config.json",
  "README.md",
  "CHANGELOG.md",
  "VERSIONING.md",
  "UPDATE_SYSTEM.md",
  "LICENSE"
)

# Create temp folder
if (Test-Path $tempFolder) {
  Remove-Item $tempFolder -Recurse -Force
}
New-Item -ItemType Directory -Path $tempFolder | Out-Null

# Copy files to temp folder
Write-Host "Copying files..." -ForegroundColor Yellow
foreach ($file in $filesToInclude) {
  if (Test-Path $file) {
    Copy-Item $file -Destination $tempFolder
    Write-Host "  ✓ $file" -ForegroundColor Green
  }
  else {
    Write-Host "  ✗ $file (not found)" -ForegroundColor Red
  }
}

# Create ZIP
Write-Host ""
Write-Host "Creating ZIP archive..." -ForegroundColor Yellow

# Remove old ZIP if exists
if (Test-Path $outputName) {
  Remove-Item $outputName -Force
}

# Create ZIP
Compress-Archive -Path "$tempFolder\*" -DestinationPath $outputName -CompressionLevel Optimal

# Cleanup temp folder
Remove-Item $tempFolder -Recurse -Force

# Done
Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "✓ Release package created successfully!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "File: $outputName" -ForegroundColor Cyan
Write-Host "Size: $([math]::Round((Get-Item $outputName).Length / 1KB, 2)) KB" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "1. Go to: https://github.com/jcardila/internet-health-monitor/releases/new" -ForegroundColor White
Write-Host "2. Drag and drop this ZIP file to the 'Attach binaries' section" -ForegroundColor White
Write-Host "3. Publish the release" -ForegroundColor White
Write-Host ""

