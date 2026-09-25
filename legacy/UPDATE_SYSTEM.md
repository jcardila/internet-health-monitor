# Sistema de Actualización Automática

Internet Health Monitor incluye un sistema inteligente de verificación de actualizaciones que funciona automáticamente.

---

## 🔄 Cómo Funciona

### **Verificación Automática al Inicio**

1. **Después de 3 segundos** de iniciar la aplicación
2. **En segundo plano** (no interrumpe el monitoreo)
3. **Compara** tu versión con la última en GitHub
4. **Notifica** si hay una actualización disponible

### **Verificación Manual**

- Haz clic en el botón **🔄** en el header de la aplicación
- O usa el atajo en el log para verificar manualmente

---

## 📊 Proceso de Verificación

```
┌─────────────────────────────────┐
│  App inicia (v1.0.0)            │
└────────────┬────────────────────┘
             │
             ▼
┌─────────────────────────────────┐
│  Espera 3 segundos              │
└────────────┬────────────────────┘
             │
             ▼
┌─────────────────────────────────┐
│  Consulta GitHub API:           │
│  api.github.com/repos/          │
│  jcardila/internet-health-      │
│  monitor/releases/latest        │
└────────────┬────────────────────┘
             │
             ▼
┌─────────────────────────────────┐
│  Compara versiones:             │
│  Latest (1.0.1) > Current (1.0.0)│
└────────────┬────────────────────┘
             │
        ┌────┴────┐
        │         │
        ▼         ▼
   [Nueva]    [Actual]
    │            │
    ▼            ▼
  [Popup]     [Log]
 "Actualizar"  "OK"
```

---

## ✅ Características del Sistema

### **No Intrusivo**

- ✅ Verificación en segundo plano
- ✅ No bloquea la aplicación
- ✅ Si falla la verificación, la app sigue funcionando
- ✅ Timeout de 5 segundos (no espera indefinidamente)

### **Seguro**

- ✅ Solo consulta la API pública de GitHub
- ✅ No descarga automáticamente
- ✅ No modifica archivos
- ✅ Usuario decide si actualizar

### **Informativo**

- ✅ Muestra versión actual vs. disponible
- ✅ Link directo a la página de descarga
- ✅ Registra en el log las verificaciones

### **Configurable**

- ✅ Se puede desactivar en `config.json`
- ✅ Botón manual siempre disponible

---

## ⚙️ Configuración

### Desactivar Verificación Automática

Edita `config.json`:

```json
{
  "advanced": {
    "checkUpdatesOnStartup": false
  }
}
```

**Nota**: El botón manual siempre estará disponible.

### Activar Verificación Automática

```json
{
  "advanced": {
    "checkUpdatesOnStartup": true
  }
}
```

---

## 🎯 Flujo de Usuario

### **Escenario 1: Actualización Disponible**

1. Usuario inicia la aplicación (v1.0.0)
2. Después de 3 segundos aparece popup:
   ```
   ┌─────────────────────────────────┐
   │ Actualización Disponible        │
   ├─────────────────────────────────┤
   │ Versión actual: v1.0.0          │
   │ Versión disponible: v1.0.1      │
   │                                 │
   │ ¿Deseas visitar la página      │
   │ de descarga?                    │
   │                                 │
   │        [Sí]        [No]         │
   └─────────────────────────────────┘
   ```
3. Si selecciona **Sí**: Se abre el navegador en GitHub releases
4. Si selecciona **No**: Continúa usando la app actual

### **Escenario 2: Versión Actual**

1. Usuario inicia la aplicación (v1.0.1)
2. En el log aparece: `✓ Versión actual: v1.0.1`
3. No se muestra popup
4. App funciona normalmente

### **Escenario 3: Sin Conexión**

1. Usuario inicia la aplicación sin internet
2. En el log aparece: `✓ Versión actual: v1.0.0`
3. No se muestra popup
4. App funciona normalmente

---

## 🔧 Verificación Manual

### **Desde la Interfaz**

1. Haz clic en el botón **🔄** en el header
2. El botón cambia a **⏳** mientras verifica
3. Resultados:
   - **Actualización disponible**: Muestra popup
   - **Versión actual**: Muestra mensaje confirmando
   - **Error**: Muestra mensaje en log

### **Desde PowerShell (Avanzado)**

```powershell
# Cargar el script
. .\InternetHealth.ps1

# Verificar actualización
$update = Test-UpdateAvailable -CurrentVersion "1.0.0"

# Ver resultado
$update
```

---

## 📡 Detalles Técnicos

### **Endpoint de GitHub**

```
GET https://api.github.com/repos/jcardila/internet-health-monitor/releases/latest
```

**Respuesta (ejemplo):**

```json
{
  "tag_name": "v1.0.1",
  "name": "Internet Health Monitor v1.0.1",
  "html_url": "https://github.com/jcardila/internet-health-monitor/releases/tag/v1.0.1",
  "body": "Release notes here..."
}
```

### **Comparación de Versiones**

```powershell
$current = [version]"1.0.0"  # 1.0.0
$latest = [version]"1.0.1"   # 1.0.1

if ($latest -gt $current) {
    # Actualización disponible
}
```

### **Timeout y Manejo de Errores**

- **Timeout**: 5 segundos
- **Error de red**: Falla silenciosamente
- **JSON inválido**: Falla silenciosamente
- **Sin internet**: No muestra error al usuario

---

## 🛡️ Privacidad y Seguridad

### **¿Qué Datos Se Envían?**

**Ninguno.** La verificación solo hace una consulta HTTP GET a la API pública de GitHub.

### **¿Qué Información Se Recibe?**

- Tag de la última versión (ej: `v1.0.1`)
- URL de descarga
- Notas de la versión

### **¿Se Descarga Algo Automáticamente?**

**No.** El usuario siempre decide:

1. Si visitar la página de descarga
2. Si descargar la nueva versión
3. Si instalar/actualizar

---

## 🔍 Solución de Problemas

### **El botón 🔄 no hace nada**

→ Revisa el log, puede haber un error de red
→ Verifica tu conexión a Internet
→ GitHub puede estar caído (raro)

### **Siempre dice "versión actual" pero sé que hay nueva versión**

→ Verifica que la versión en el header sea correcta:

```powershell
Get-Content InternetHealth.ps1 | Select-String "# Version:"
```

→ Verifica que exista un release en GitHub

### **Quiero desactivar las verificaciones**

→ Edita `config.json`:

```json
{ "advanced": { "checkUpdatesOnStartup": false } }
```

### **Error: "No se pudo verificar actualizaciones"**

Causas comunes:

- Sin conexión a Internet
- Firewall bloqueando GitHub
- GitHub API temporalmente no disponible
- Rate limit de GitHub (60 requests/hora sin autenticación)

**Solución**: Espera unos minutos y vuelve a intentar manualmente.

---

## 📋 Mejores Prácticas para Usuarios

### **Usuarios Normales**

1. ✅ Deja `checkUpdatesOnStartup: true`
2. ✅ Actualiza cuando se te notifique
3. ✅ Lee las notas de versión antes de actualizar

### **Usuarios en Redes Corporativas**

1. ⚠️ Tu firewall puede bloquear GitHub
2. ⚠️ Configura `checkUpdatesOnStartup: false`
3. ✅ Verifica manualmente periódicamente

### **Usuarios Sin Internet Frecuente**

1. ✅ Configura `checkUpdatesOnStartup: false`
2. ✅ Verifica manualmente cuando tengas internet
3. ✅ Descarga versiones nuevas cuando sea posible

---

## 🚀 Para Desarrolladores

### **Agregar Verificación a Tu Script**

```powershell
# Verificar actualización
$updateInfo = Test-UpdateAvailable -CurrentVersion $script:AppVersion

if ($updateInfo.Available) {
    Write-Host "Nueva versión: $($updateInfo.LatestVersion)"
    Write-Host "Descargar: $($updateInfo.DownloadUrl)"
}
```

### **Mostrar UI**

```powershell
Test-UpdateAvailable -CurrentVersion "1.0.0" -ShowUI
```

### **Testing**

```powershell
# Simular versión antigua
Test-UpdateAvailable -CurrentVersion "0.0.1" -ShowUI
```

---

## 📊 Estadísticas

El sistema de actualización:

- ✅ No recopila estadísticas
- ✅ No envía telemetría
- ✅ No rastrea usuarios
- ✅ Solo consulta versión pública en GitHub

---

## 🔗 Referencias

- [GitHub API - Releases](https://docs.github.com/en/rest/releases/releases)
- [Semantic Versioning](https://semver.org/)
- [VERSIONING.md](VERSIONING.md) - Proceso de release
- [CHANGELOG.md](CHANGELOG.md) - Historial de cambios
