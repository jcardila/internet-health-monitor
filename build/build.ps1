<#
.SYNOPSIS
  Compila, prueba y empaqueta Monitor de Conexión.

.EXAMPLE
  .\build\build.ps1                         # pruebas + publicación en artifacts\publish
  .\build\build.ps1 -Version 2.0.1 -Pack    # además crea el instalador (Setup.exe) y los paquetes de actualización
  .\build\build.ps1 -Version 2.0.1 -Pack -SignParams '/a /fd sha256 /tr http://timestamp.digicert.com /td sha256'

.NOTES
  Requiere el SDK de .NET 10 (winget install Microsoft.DotNet.SDK.10).
  -Pack instala/actualiza la herramienta 'vpk' de Velopack (dotnet tool).
#>
param(
    [string]$Version = "2.0.0",
    [switch]$Pack,
    [string]$SignParams = "",
    [switch]$SkipTests,
    # Debe coincidir con la versión del paquete Velopack en InternetHealth.App.csproj.
    [string]$VpkVersion = "1.2.158",
    # Con -Pack: descarga antes el último release de este repositorio para generar el paquete
    # delta (actualización pequeña) y un releases.win.json con el historial.
    [string]$GithubRepo = "",
    [string]$GithubToken = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
$artifacts = Join-Path $root "artifacts"
$publish = Join-Path $artifacts "publish"
$releases = Join-Path $artifacts "releases"

function Step($text) { Write-Host "`n==> $text" -ForegroundColor Cyan }

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "No se encontró el SDK de .NET. Instálalo con: winget install Microsoft.DotNet.SDK.10"
}

# La app en ejecución bloquea sus archivos: la cerramos antes de compilar.
Get-Process InternetHealthMonitor -ErrorAction SilentlyContinue | Stop-Process -Force

if (-not $SkipTests) {
    Step "Pruebas automáticas"
    dotnet run --project tests/InternetHealth.Core.Tests -c Release
    if ($LASTEXITCODE -ne 0) { throw "Las pruebas fallaron." }
}

Step "Publicando versión $Version (autocontenida, win-x64)"
if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }
dotnet publish src/InternetHealth.App/InternetHealth.App.csproj -c Release -r win-x64 --self-contained `
    -p:Version=$Version -p:PublishReadyToRun=true -o $publish
if ($LASTEXITCODE -ne 0) { throw "La publicación falló." }

$size = (Get-ChildItem $publish -Recurse | Measure-Object Length -Sum).Sum / 1MB
Write-Host ("Publicado en {0} ({1:N1} MB)" -f $publish, $size) -ForegroundColor Green

if ($Pack) {
    Step "Creando instalador y paquetes de actualización (Velopack)"
    dotnet tool update -g vpk --version $VpkVersion | Out-Null
    $tools = Join-Path $env:USERPROFILE ".dotnet\tools"
    if ($env:PATH -notlike "*$tools*") { $env:PATH += ";$tools" } # recién instalada, en una consola nueva o en CI
    if ($GithubRepo) {
        $dlArgs = @("download", "github", "--repoUrl", $GithubRepo, "--outputDir", $releases)
        if ($GithubToken) { $dlArgs += @("--token", $GithubToken) }
        vpk @dlArgs
        if ($LASTEXITCODE -ne 0) { Write-Host "No se encontró un release anterior: se crea solo el paquete completo." -ForegroundColor Yellow }
    }
    $vpkArgs = @(
        "pack",
        "--packId", "InternetHealthMonitor",
        "--packVersion", $Version,
        "--packDir", $publish,
        "--mainExe", "InternetHealthMonitor.exe",
        "--runtime", "win-x64",
        "--packTitle", "Monitor de Conexión",
        "--packAuthors", "Grupo Ardisa",
        "--icon", "src/InternetHealth.App/Assets/app.ico",
        "--outputDir", $releases
    )
    if ($SignParams) { $vpkArgs += @("--signParams", $SignParams) }
    vpk @vpkArgs
    if ($LASTEXITCODE -ne 0) { throw "vpk pack falló." }

    Write-Host "`nListo. En $releases encontrarás:" -ForegroundColor Green
    Write-Host "  - InternetHealthMonitor-win-Setup.exe  → instalador para los usuarios (no requiere administrador)"
    Write-Host "  - releases.win.json + *.nupkg           → súbelos a la URL de 'updateFeedUrl' para las actualizaciones automáticas"
}
