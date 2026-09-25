# Compila y prueba la app en este equipo. Guarda el registro en artifacts\compilacion.log
# y, si todo sale bien, abre la app en modo demostración.
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
New-Item -ItemType Directory -Force -Path "$root\artifacts" | Out-Null
$log = "$root\artifacts\compilacion.log"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:DOTNET_NOLOGO = "1"
Set-Content -Path $log -Value "=== Compilacion $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') ===" -Encoding ASCII

function Run($title, $cmdline) {
    Write-Host "`n==> $title" -ForegroundColor Cyan
    Add-Content -Path $log -Value "`r`n==> $title" -Encoding ASCII
    cmd /c "$cmdline >> ""$log"" 2>&1"
    $code = $LASTEXITCODE
    Add-Content -Path $log -Value "(codigo de salida: $code)" -Encoding ASCII
    return $code
}

$sdks = (& dotnet --list-sdks 2>$null) -join "`n"
if ($sdks -notmatch '(?m)^10\.') {
    Write-Host "No se encontró el SDK de .NET 10. Instalando con winget (puede pedir confirmación)..." -ForegroundColor Yellow
    Run "Instalando SDK .NET 10" @("winget", "install", "--id", "Microsoft.DotNet.SDK.10", "--exact", "--silent", "--accept-package-agreements", "--accept-source-agreements") | Out-Null
    $env:PATH = [Environment]::GetEnvironmentVariable("PATH", "Machine") + ";" + [Environment]::GetEnvironmentVariable("PATH", "User")
}

# La app es de instancia única y su .exe queda bloqueado mientras corre: la cerramos antes de compilar.
$running = Get-Process InternetHealthMonitor -ErrorAction SilentlyContinue
if ($running) {
    Write-Host "Cerrando la instancia abierta de la app..." -ForegroundColor Yellow
    $running | Stop-Process -Force
    Start-Sleep -Seconds 1
}

$t = Run "Pruebas del nucleo" @("dotnet", "run", "--project", "tests/InternetHealth.Core.Tests", "-c", "Release")
$b = Run "Compilando la app" @("dotnet", "build", "src/InternetHealth.App/InternetHealth.App.csproj", "-c", "Debug", "-nologo", "-v:minimal")

if ($b -eq 0) {
    Add-Content -Path $log -Value "`r`nRESULTADO: OK (pruebas=$t, compilacion=$b)" -Encoding ASCII
    Write-Host "`nCompilación correcta. Abriendo la app en modo demostración..." -ForegroundColor Green
    $exe = Get-ChildItem "$root\src\InternetHealth.App\bin\Debug" -Recurse -Filter InternetHealthMonitor.exe | Select-Object -First 1
    if ($exe) { Start-Process $exe.FullName -ArgumentList "--demo" }
} else {
    Add-Content -Path $log -Value "`r`nRESULTADO: ERROR (pruebas=$t, compilacion=$b)" -Encoding ASCII
    Write-Host "`nLa compilación falló. Últimos errores:" -ForegroundColor Red
    Select-String -Path $log -Pattern " error " | Select-Object -Last 8 | ForEach-Object { Write-Host $_.Line }
}
Write-Host "`nPruebas: $(if ($t -eq 0) {'OK'} else {'con fallas'}) | Compilación: $(if ($b -eq 0) {'OK'} else {'con errores'})"
Write-Host "Puedes cerrar esta ventana."
