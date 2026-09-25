# ===================================================================
# Build Script for Internet Health Monitor Executable
# ===================================================================
# This script compiles InternetHealth.ps1 to a standalone .exe
# Requires: ps2exe module (Install-Module ps2exe)
# ===================================================================

param(
  [string]$Version = ""
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Internet Health Monitor - Build Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Auto-detect version from script if not provided
if ([string]::IsNullOrEmpty($Version)) {
  $scriptContent = Get-Content "InternetHealth.ps1" -Raw
  if ($scriptContent -match '# Version: ([\d\.]+)') {
    $Version = $matches[1]
    Write-Host "Auto-detected version: $Version" -ForegroundColor Green
  }
  else {
    Write-Host "ERROR: Could not detect version from InternetHealth.ps1" -ForegroundColor Red
    Write-Host "Please provide version: .\build-exe.ps1 -Version '1.0.0'" -ForegroundColor Yellow
    exit 1
  }
}

Write-Host "Building version: $Version" -ForegroundColor Cyan
Write-Host ""

# Check if ps2exe is installed
if (-not (Get-Command ps2exe -ErrorAction SilentlyContinue)) {
  Write-Host "ERROR: ps2exe is not installed" -ForegroundColor Red
  Write-Host ""
  Write-Host "Install with:" -ForegroundColor Yellow
  Write-Host "  Install-Module -Name ps2exe -Scope CurrentUser" -ForegroundColor White
  Write-Host ""
  Write-Host "Or visit: https://github.com/MScholtes/PS2EXE" -ForegroundColor Gray
  exit 1
}

# Check if input file exists
if (-not (Test-Path "InternetHealth.ps1")) {
  Write-Host "ERROR: InternetHealth.ps1 not found" -ForegroundColor Red
  exit 1
}

# Check if icon exists
$iconExists = Test-Path "icon.ico"
if (-not $iconExists) {
  Write-Host "WARNING: icon.ico not found, building without custom icon" -ForegroundColor Yellow
  Write-Host "Create an icon and save as 'icon.ico' for branded executable" -ForegroundColor Gray
  Write-Host ""
}

# Create build directory
if (-not (Test-Path "build")) {
  New-Item -ItemType Directory -Path "build" | Out-Null
  Write-Host "Created build directory" -ForegroundColor Green
}

# Prepare build parameters
$buildParams = @{
  inputFile    = ".\InternetHealth.ps1"
  outputFile   = ".\build\InternetHealthMonitor.exe"
  title        = "Internet Health Monitor"
  description  = "Monitor de salud de conexión a Internet - Diagnostica problemas de red en tiempo real"
  company      = "jcardila"
  product      = "Internet Health Monitor"
  copyright    = "Copyright (c) 2025 jcardila - MIT License"
  version      = "$Version.0"
  noConsole    = $true
  requireAdmin = $false
  supportOS    = $true
  longPaths    = $true
}

# Add icon if exists
if ($iconExists) {
  $buildParams.iconFile = ".\icon.ico"
}

# Build
Write-Host "Compiling executable..." -ForegroundColor Yellow
Write-Host ""

try {
  ps2exe @buildParams
    
  if (Test-Path ".\build\InternetHealthMonitor.exe") {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "✓ Build successful!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host ""
        
    $fileInfo = Get-Item ".\build\InternetHealthMonitor.exe"
    Write-Host "Output file:" -ForegroundColor Cyan
    Write-Host "  .\build\InternetHealthMonitor.exe" -ForegroundColor White
    Write-Host ""
    Write-Host "File size:" -ForegroundColor Cyan
    Write-Host "  $([math]::Round($fileInfo.Length / 1MB, 2)) MB" -ForegroundColor White
    Write-Host ""
    Write-Host "Version:" -ForegroundColor Cyan
    Write-Host "  $Version" -ForegroundColor White
    Write-Host ""
        
    # Show properties
    $versionInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($fileInfo.FullName)
    Write-Host "Properties:" -ForegroundColor Cyan
    Write-Host "  Company: $($versionInfo.CompanyName)" -ForegroundColor Gray
    Write-Host "  Product: $($versionInfo.ProductName)" -ForegroundColor Gray
    Write-Host "  Description: $($versionInfo.FileDescription)" -ForegroundColor Gray
    Write-Host ""
        
    # Test run instructions
    Write-Host "========================================" -ForegroundColor Yellow
    Write-Host "Next steps:" -ForegroundColor Yellow
    Write-Host "========================================" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "1. Test the executable:" -ForegroundColor White
    Write-Host "   .\build\InternetHealthMonitor.exe" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "2. Create distribution package:" -ForegroundColor White
    Write-Host "   .\create-exe-package.ps1 -Version $Version" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "3. (Optional) Sign the executable:" -ForegroundColor White
    Write-Host "   See BUILD_EXE.md for signing instructions" -ForegroundColor Cyan
    Write-Host ""
        
  }
  else {
    Write-Host "ERROR: Build completed but output file not found" -ForegroundColor Red
    exit 1
  }
}
catch {
  Write-Host ""
  Write-Host "========================================" -ForegroundColor Red
  Write-Host "✗ Build failed" -ForegroundColor Red
  Write-Host "========================================" -ForegroundColor Red
  Write-Host ""
  Write-Host "Error details:" -ForegroundColor Yellow
  Write-Host $_.Exception.Message -ForegroundColor Gray
  Write-Host ""
  Write-Host "Stack trace:" -ForegroundColor Yellow
  Write-Host $_.ScriptStackTrace -ForegroundColor Gray
  exit 1
}

