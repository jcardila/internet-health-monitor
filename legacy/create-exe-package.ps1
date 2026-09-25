# ===================================================================
# Create EXE Distribution Package for Internet Health Monitor
# ===================================================================
# This script creates a ZIP file with the compiled .exe and docs
# ===================================================================

param(
  [string]$Version = ""
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Creating EXE Distribution Package" -ForegroundColor Cyan
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
    Write-Host "ERROR: Could not detect version" -ForegroundColor Red
    exit 1
  }
}

$exePath = ".\build\InternetHealthMonitor.exe"
$outputName = "InternetHealthMonitor-v$Version-exe.zip"
$tempFolder = "package-temp"

# Check if exe exists
if (-not (Test-Path $exePath)) {
  Write-Host "ERROR: $exePath not found" -ForegroundColor Red
  Write-Host "Build the executable first with: .\build-exe.ps1" -ForegroundColor Yellow
  exit 1
}

# Files to include with the .exe
$filesToInclude = @(
  @{ Path = $exePath; DestName = "InternetHealthMonitor.exe" },
  @{ Path = "config.json"; DestName = "config.json" },
  @{ Path = "README.md"; DestName = "README.md" },
  @{ Path = "CHANGELOG.md"; DestName = "CHANGELOG.md" },
  @{ Path = "LICENSE"; DestName = "LICENSE" }
)

# Create temp folder
if (Test-Path $tempFolder) {
  Remove-Item $tempFolder -Recurse -Force
}
New-Item -ItemType Directory -Path $tempFolder | Out-Null

# Copy files
Write-Host "Packaging files..." -ForegroundColor Yellow
foreach ($file in $filesToInclude) {
  if (Test-Path $file.Path) {
    Copy-Item $file.Path -Destination "$tempFolder\$($file.DestName)"
    Write-Host "  ✓ $($file.DestName)" -ForegroundColor Green
  }
  else {
    Write-Host "  ✗ $($file.Path) (not found)" -ForegroundColor Red
  }
}

# Create README-EXE.txt with specific instructions for exe version
$exeReadme = @"
========================================
INTERNET HEALTH MONITOR v$Version
Versión Ejecutable (.exe)
========================================

¡Gracias por descargar Internet Health Monitor!

INICIO RÁPIDO:
---------------
1. Doble clic en "InternetHealthMonitor.exe"
2. ¡Listo! La aplicación se iniciará automáticamente

Si Windows SmartScreen aparece:
  → Haz clic en "Más información"
  → Luego "Ejecutar de todas formas"

Esto es normal para aplicaciones sin firma digital.
El código fuente está disponible en GitHub para revisión.

CONFIGURACIÓN:
---------------
Edita el archivo "config.json" para personalizar:
- Intervalos de ping
- Umbrales de latencia
- Servidores DNS a probar
- Activar/desactivar verificación de actualizaciones

DOCUMENTACIÓN COMPLETA:
------------------------
README.md - Guía completa de uso
CHANGELOG.md - Historial de versiones

REPORTAR PROBLEMAS:
-------------------
Usa el botón "🐛 Reportar Problema" en la app
o visita: https://github.com/jcardila/internet-health-monitor/issues

LICENCIA:
---------
MIT License - Ver archivo LICENSE

AUTOR:
------
jcardila
https://github.com/jcardila

========================================
"@

$exeReadme | Out-File "$tempFolder\LÉEME-EXE.txt" -Encoding UTF8
Write-Host "  ✓ LÉEME-EXE.txt (created)" -ForegroundColor Green

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
Write-Host "✓ EXE package created successfully!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Package: $outputName" -ForegroundColor Cyan
Write-Host "Size: $([math]::Round((Get-Item $outputName).Length / 1MB, 2)) MB" -ForegroundColor Cyan
Write-Host ""
Write-Host "Contents:" -ForegroundColor Yellow
Write-Host "  - InternetHealthMonitor.exe (standalone executable)" -ForegroundColor White
Write-Host "  - config.json (configuration file)" -ForegroundColor White
Write-Host "  - LÉEME-EXE.txt (quick start guide)" -ForegroundColor White
Write-Host "  - README.md (full documentation)" -ForegroundColor White
Write-Host "  - CHANGELOG.md (version history)" -ForegroundColor White
Write-Host "  - LICENSE (MIT license)" -ForegroundColor White
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "1. Test the package by extracting and running the .exe" -ForegroundColor White
Write-Host "2. Upload to GitHub Release as an asset" -ForegroundColor White
Write-Host "3. Update release notes to mention both versions available" -ForegroundColor White
Write-Host ""

