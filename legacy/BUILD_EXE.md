# Guía para Convertir a Ejecutable (.EXE)

Esta guía explica cómo convertir `InternetHealth.ps1` a un archivo `.exe` standalone para máxima facilidad de distribución.

---

## 🎯 Beneficios de Crear un .EXE

### **Para Usuarios:**

- ✅ **Doble clic y listo** - No necesita explicar PowerShell
- ✅ **Más profesional** - Parece una app "real"
- ✅ **No necesita RUN_ME.bat** - Un solo archivo ejecutable
- ✅ **Menos confusión** - No ven el código fuente
- ✅ **Icono personalizado** - Se ve bien en el escritorio

### **Para Distribución:**

- ✅ **Más confianza** - Los usuarios confían más en .exe
- ✅ **Menos soporte** - Menos preguntas sobre "cómo ejecutar"
- ✅ **Windows SmartScreen** - Puedes firmar digitalmente el .exe
- ✅ **Instaladores** - Puedes crear un instalador MSI/NSIS

---

## 🛠️ Opciones de Herramientas

### **Opción 1: PS2EXE (Recomendada)** ⭐

**Ventajas:**

- ✅ Más popular y probada
- ✅ Activamente mantenida
- ✅ Gratis y open source
- ✅ Soporta iconos personalizados
- ✅ Puede ocultar la ventana de consola

**Desventajas:**

- ⚠️ Algunos antivirus pueden dar falsos positivos
- ⚠️ El .exe es más grande (~2-3 MB)

### **Opción 2: PS2EXE-GUI**

Similar a PS2EXE pero con interfaz gráfica para configurar opciones.

### **Opción 3: PowerShell Studio** 💰

**Ventajas:**

- ✅ IDE completo con debugger
- ✅ GUI designer
- ✅ Firma digital incluida
- ✅ Muy profesional

**Desventajas:**

- ❌ De pago ($389 USD)
- ❌ Overkill para proyectos pequeños

---

## 📦 Método Recomendado: PS2EXE

### **Paso 1: Instalar PS2EXE**

```powershell
# Opción A: Desde PowerShell Gallery (recomendado)
Install-Module -Name ps2exe -Scope CurrentUser

# Opción B: Desde GitHub
# https://github.com/MScholtes/PS2EXE
```

### **Paso 2: Crear el .EXE**

#### **Básico (sin opciones):**

```powershell
ps2exe .\InternetHealth.ps1 .\InternetHealthMonitor.exe
```

#### **Completo (con todas las opciones):**

```powershell
ps2exe `
    -inputFile ".\InternetHealth.ps1" `
    -outputFile ".\InternetHealthMonitor.exe" `
    -title "Internet Health Monitor" `
    -description "Monitor de salud de conexión a Internet" `
    -company "jcardila" `
    -product "Internet Health Monitor" `
    -copyright "Copyright (c) 2025 jcardila" `
    -version "1.0.0.0" `
    -iconFile ".\icon.ico" `
    -noConsole `
    -requireAdmin:$false `
    -supportOS `
    -longPaths
```

#### **Explicación de Parámetros:**

| Parámetro       | Descripción                | Recomendado               |
| --------------- | -------------------------- | ------------------------- |
| `-inputFile`    | Script PowerShell origen   | ✅ Requerido              |
| `-outputFile`   | Archivo .exe destino       | ✅ Requerido              |
| `-title`        | Título en propiedades      | ✅ Sí                     |
| `-description`  | Descripción del programa   | ✅ Sí                     |
| `-company`      | Nombre de compañía/autor   | ✅ Sí                     |
| `-product`      | Nombre del producto        | ✅ Sí                     |
| `-copyright`    | Copyright                  | ✅ Sí                     |
| `-version`      | Versión (formato: X.Y.Z.B) | ✅ Sí                     |
| `-iconFile`     | Icono personalizado (.ico) | ✅ Sí                     |
| `-noConsole`    | Ocultar ventana de consola | ✅ Sí (GUI app)           |
| `-requireAdmin` | Requiere admin             | ❌ No (no lo necesitamos) |
| `-supportOS`    | Windows 7+ compatibility   | ✅ Sí                     |
| `-longPaths`    | Soporta rutas largas       | ✅ Sí                     |

### **Paso 3: Crear un Icono (.ico)**

#### **Opción A: Convertir desde PNG**

Usa herramientas online:

- https://www.icoconverter.com/
- https://convertio.co/png-ico/

**Tamaños recomendados:** 16x16, 32x32, 48x48, 256x256 (multi-size)

#### **Opción B: Crear con GIMP/Photoshop**

Exporta como `.ico` con múltiples tamaños.

#### **Opción C: Usar Icono Existente**

```powershell
# Extraer icono de otro programa
# Usa Icon Extractor: https://www.nirsoft.net/utils/iconsext.html
```

### **Paso 4: Script Automatizado**

Crea `build-exe.ps1`:

```powershell
# build-exe.ps1
# Automated build script for Internet Health Monitor

param(
    [string]$Version = "1.0.0"
)

Write-Host "Building Internet Health Monitor v$Version..." -ForegroundColor Cyan

# Check if ps2exe is installed
if (-not (Get-Command ps2exe -ErrorAction SilentlyContinue)) {
    Write-Host "ERROR: ps2exe is not installed" -ForegroundColor Red
    Write-Host "Install with: Install-Module -Name ps2exe -Scope CurrentUser" -ForegroundColor Yellow
    exit 1
}

# Check if icon exists
if (-not (Test-Path "icon.ico")) {
    Write-Host "WARNING: icon.ico not found, building without icon" -ForegroundColor Yellow
    $iconParam = @{}
} else {
    $iconParam = @{ iconFile = ".\icon.ico" }
}

# Build parameters
$buildParams = @{
    inputFile = ".\InternetHealth.ps1"
    outputFile = ".\build\InternetHealthMonitor.exe"
    title = "Internet Health Monitor"
    description = "Monitor de salud de conexión a Internet - Diagnostica problemas de red"
    company = "jcardila"
    product = "Internet Health Monitor"
    copyright = "Copyright (c) 2025 jcardila"
    version = "$Version.0"
    noConsole = $true
    requireAdmin = $false
    supportOS = $true
    longPaths = $true
}

# Add icon if exists
if ($iconParam.Count -gt 0) {
    $buildParams += $iconParam
}

# Create build directory
if (-not (Test-Path "build")) {
    New-Item -ItemType Directory -Path "build" | Out-Null
}

# Build
try {
    ps2exe @buildParams

    if (Test-Path ".\build\InternetHealthMonitor.exe") {
        Write-Host ""
        Write-Host "✓ Build successful!" -ForegroundColor Green
        Write-Host ""
        Write-Host "Output: .\build\InternetHealthMonitor.exe" -ForegroundColor Cyan

        $fileInfo = Get-Item ".\build\InternetHealthMonitor.exe"
        Write-Host "Size: $([math]::Round($fileInfo.Length / 1MB, 2)) MB" -ForegroundColor Cyan
        Write-Host ""

        # Test run
        Write-Host "Test the executable with:" -ForegroundColor Yellow
        Write-Host "  .\build\InternetHealthMonitor.exe" -ForegroundColor White
    }
}
catch {
    Write-Host "ERROR: Build failed" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Gray
    exit 1
}
```

**Uso:**

```powershell
.\build-exe.ps1 -Version "1.0.0"
```

---

## 🔒 Firma Digital del .EXE (Opcional pero Recomendado)

### **¿Por qué firmar?**

- ✅ Menos warnings de Windows SmartScreen
- ✅ Mayor confianza de usuarios
- ✅ Antivirus más confiados
- ✅ Más profesional

### **Opciones:**

#### **1. Certificado de Code Signing Comercial** (💰 ~$70-300/año)

Proveedores:

- DigiCert
- Sectigo
- GlobalSign

**Proceso:**

```powershell
# Firmar el .exe
signtool sign /f "cert.pfx" /p "password" /t http://timestamp.digicert.com ".\InternetHealthMonitor.exe"
```

#### **2. Certificado Auto-Firmado** (Gratis, pero usuarios necesitan instalar cert)

```powershell
# Crear certificado
$cert = New-SelfSignedCertificate -Subject "CN=jcardila" -Type CodeSigning -CertStoreLocation Cert:\CurrentUser\My

# Exportar
Export-Certificate -Cert $cert -FilePath ".\jcardila.cer"

# Firmar
Set-AuthenticodeSignature -FilePath ".\InternetHealthMonitor.exe" -Certificate $cert
```

**Desventaja:** Usuarios verán "Unknown Publisher" hasta que instalen tu certificado.

#### **3. Sin Certificado (Gratis)**

El .exe funcionará pero:

- ⚠️ Windows SmartScreen mostrará warning
- ⚠️ Usuarios deberán hacer "Run anyway"
- ⚠️ Algunos antivirus pueden detectar como sospechoso

**Solución:** En README explica que es normal para apps sin firma.

---

## 📦 Crear Paquete de Distribución

### **Opción A: ZIP Simple**

```powershell
# Crear ZIP con el .exe
Compress-Archive -Path @(
    ".\build\InternetHealthMonitor.exe",
    ".\config.json",
    ".\README.md",
    ".\LICENSE"
) -DestinationPath ".\InternetHealthMonitor-v1.0.0-exe.zip"
```

### **Opción B: Instalador NSIS** (Más profesional)

Usa [NSIS](https://nsis.sourceforge.io/) para crear un instalador `.exe`:

```nsis
; install.nsi
!define APPNAME "Internet Health Monitor"
!define COMPANYNAME "jcardila"
!define DESCRIPTION "Monitor de salud de conexión a Internet"
!define VERSIONMAJOR 1
!define VERSIONMINOR 0
!define VERSIONBUILD 0

Name "${APPNAME}"
OutFile "InternetHealthMonitor-Setup-v1.0.0.exe"
InstallDir "$PROGRAMFILES\${APPNAME}"

Page directory
Page instfiles

Section "Install"
    SetOutPath $INSTDIR
    File "InternetHealthMonitor.exe"
    File "config.json"
    File "README.md"
    File "LICENSE"

    CreateShortCut "$DESKTOP\${APPNAME}.lnk" "$INSTDIR\InternetHealthMonitor.exe"
    CreateDirectory "$SMPROGRAMS\${APPNAME}"
    CreateShortCut "$SMPROGRAMS\${APPNAME}\${APPNAME}.lnk" "$INSTDIR\InternetHealthMonitor.exe"

    WriteUninstaller "$INSTDIR\Uninstall.exe"
SectionEnd

Section "Uninstall"
    Delete "$INSTDIR\InternetHealthMonitor.exe"
    Delete "$INSTDIR\config.json"
    Delete "$INSTDIR\README.md"
    Delete "$INSTDIR\LICENSE"
    Delete "$INSTDIR\Uninstall.exe"
    Delete "$DESKTOP\${APPNAME}.lnk"
    Delete "$SMPROGRAMS\${APPNAME}\${APPNAME}.lnk"
    RMDir "$SMPROGRAMS\${APPNAME}"
    RMDir "$INSTDIR"
SectionEnd
```

Compilar:

```bash
makensis install.nsi
```

---

## 🧪 Testing del .EXE

### **Checklist de Pruebas:**

- [ ] El .exe se ejecuta correctamente
- [ ] La GUI se muestra sin errores
- [ ] config.json se carga correctamente
- [ ] Todas las funcionalidades funcionan
- [ ] Exportar reporte funciona
- [ ] Botón de actualización funciona
- [ ] No hay ventanas de consola extra
- [ ] El icono se ve correctamente
- [ ] Las propiedades del archivo son correctas
- [ ] Funciona en Windows 10 limpio
- [ ] Funciona en Windows 11

### **Testing en Máquina Virtual:**

```powershell
# Probar en VM limpia de Windows
# - Sin PowerShell 7
# - Sin desarrollo tools
# - Usuario normal (sin admin)
```

---

## 🐛 Problemas Comunes

### **"Cannot find type [Windows.Markup.XamlReader]"**

**Causa:** .NET Framework no disponible

**Solución:** Asegúrate de incluir `-supportOS` en ps2exe

### **El .exe es enorme (>10 MB)**

**Causa:** ps2exe incluye todo PowerShell

**Solución:** Normal para ps2exe. Alternativas:

- Compilar con .NET 6+ (más complejo)
- Aceptar el tamaño (2-4 MB es razonable)

### **Antivirus detecta como malware**

**Causa:** Falso positivo común con ps2exe

**Soluciones:**

1. Firma digital (mejor opción)
2. Reporta falso positivo a antivirus
3. Documenta en README que es esperado

### **"The file is not digitally signed"**

**Solución:** Explica en README cómo hacer "Run anyway"

---

## 📊 Comparación: .ps1 vs .exe

| Aspecto              | .ps1 Script             | .exe Compilado             |
| -------------------- | ----------------------- | -------------------------- |
| **Facilidad de uso** | ⚠️ Requiere explicación | ✅ Doble clic              |
| **Tamaño**           | ✅ ~50 KB               | ⚠️ ~2-4 MB                 |
| **Confianza**        | ⚠️ "Es un script..."    | ✅ "Es una app"            |
| **Modificable**      | ✅ Código visible       | ❌ Compilado               |
| **SmartScreen**      | ⚠️ Warning              | ⚠️ Warning (sin firma)     |
| **Distribución**     | ⚠️ Más archivos         | ✅ Un solo .exe            |
| **Debug**            | ✅ Fácil                | ⚠️ Más difícil             |
| **Updates**          | ✅ Simple               | ✅ Simple (download nuevo) |

---

## 🎯 Recomendación

### **Para v1.0.0:**

Distribuye **ambas versiones**:

1. **InternetHealthMonitor-v1.0.0.zip** (script)

   - Para usuarios técnicos
   - Para debugging
   - Para contribuciones

2. **InternetHealthMonitor-v1.0.0-exe.zip** (ejecutable)
   - Para usuarios finales
   - Más fácil de usar
   - Más profesional

### **En GitHub Releases:**

```
Assets:
  - InternetHealthMonitor-v1.0.0.zip (634 KB) - Script version
  - InternetHealthMonitor-v1.0.0-exe.zip (2.4 MB) - Executable version
  - Source code (zip)
  - Source code (tar.gz)
```

---

## 🚀 Próximos Pasos

1. **Crea un icono** para la app
2. **Instala ps2exe**: `Install-Module ps2exe`
3. **Crea build-exe.ps1** con el script de arriba
4. **Compila**: `.\build-exe.ps1 -Version "1.0.0"`
5. **Prueba** el .exe en una VM limpia
6. **Opcional:** Firma el .exe
7. **Crea release** con ambas versiones

---

## 📚 Referencias

- [PS2EXE GitHub](https://github.com/MScholtes/PS2EXE)
- [PowerShell Studio](https://www.sapien.com/software/powershell_studio)
- [NSIS](https://nsis.sourceforge.io/)
- [Code Signing Tutorial](https://docs.microsoft.com/en-us/windows/win32/seccrypto/using-signtool)
- [Icon Converter](https://www.icoconverter.com/)

