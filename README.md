# Internet Health Monitor

<div align="center">

![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-lightgrey.svg)
![PowerShell](https://img.shields.io/badge/PowerShell-5.1%2B-blue.svg)
![License](https://img.shields.io/badge/license-MIT-green.svg)

**Una herramienta visual y amigable para diagnosticar problemas de conexión a Internet en Windows**

[Características](#características) • [Instalación](#instalación) • [Uso](#uso) • [Configuración](#configuración) • [Solución de Problemas](#solución-de-problemas)

</div>

---

## 📋 Descripción

**Internet Health Monitor** es una herramienta de diagnóstico de red con interfaz gráfica que te ayuda a identificar si tus problemas de Internet son causados por:

- 🔴 **Tu conexión WiFi/Ethernet al router** (problema local)
- 🌐 **El servicio de Internet del proveedor** (problema externo)

La aplicación monitorea en tiempo real ambas conexiones y proporciona diagnósticos claros con pasos específicos para solucionar el problema.

---

## ✨ Características

### Monitoreo en Tiempo Real

- ⚡ **Ping al Router**: Mide la calidad de tu conexión local (WiFi/Ethernet)
- 🌍 **Ping a Internet**: Mide la calidad de tu conexión a Internet
- 📊 **Gráficos de Latencia**: Visualización en tiempo real con código de colores (verde/amarillo/rojo)
- 📈 **Estadísticas**: Promedio de latencia y pérdida efectiva de paquetes

### Diagnóstico Inteligente

- 🧠 **Análisis Automático**: Identifica dónde está el problema (router vs. Internet)
- 💡 **Recomendaciones Paso a Paso**: Te dice exactamente qué hacer para resolver el problema
- 🎯 **Priorización Correcta**: Si el router falla, te indica que debes arreglarlo primero antes de culpar al proveedor

### Funcionalidades Adicionales

- 📝 **Registro Detallado**: Historial de todos los eventos de red
- 💾 **Exportar Reportes**: Guarda informes completos con estadísticas y logs
- 🎨 **Interfaz Moderna**: Diseño oscuro profesional y fácil de leer
- ⚙️ **Altamente Configurable**: Ajusta umbrales, intervalos y targets según tus necesidades
- 🔄 **Auto-Actualización**: Verifica automáticamente si hay nuevas versiones disponibles

---

## 🖥️ Requisitos del Sistema

### Requisitos Mínimos

| Componente            | Requisito                                         |
| --------------------- | ------------------------------------------------- |
| **Sistema Operativo** | Windows 10 (versión 1809 o superior) o Windows 11 |
| **PowerShell**        | Versión 5.1 o superior (incluido en Windows)      |
| **.NET Framework**    | 4.7.2 o superior (incluido en Windows)            |
| **Permisos**          | No requiere privilegios de administrador          |
| **RAM**               | 50 MB de RAM disponible                           |
| **Espacio en Disco**  | < 1 MB                                            |

### Probado En

✅ **Windows 11** (Build 26100)  
✅ **PowerShell 7.x**  
✅ **.NET Framework 4.7.2+**

> **Nota**: Aunque solo se ha probado en Windows 11 con PowerShell 7.x, debería funcionar sin problemas en Windows 10 (1809+) con PowerShell 5.1+.

---

## 📥 Instalación

### Opción 1: Descarga Directa (Recomendada)

1. **Descarga todos los archivos** del repositorio
2. **Extrae** el contenido en una carpeta de tu elección (ej: `C:\Tools\InternetHealthMonitor\`)
3. ¡Listo! No requiere instalación adicional

### Opción 2: Git Clone

```bash
git clone https://github.com/jcardila/internet-health-monitor.git
cd internet-health-monitor
```

### Estructura de Archivos

```
internet-health-monitor/
├── InternetHealth.ps1    # Script principal
├── RUN_ME.bat           # Launcher (recomendado para ejecutar)
├── config.json          # Archivo de configuración (opcional)
├── README.md            # Documentación completa
├── README.es.md         # Guía rápida en español
├── CHANGELOG.md         # Historial de versiones
├── VERSIONING.md        # Guía de versionamiento
├── UPDATE_SYSTEM.md     # Sistema de auto-actualización
├── LICENSE              # Licencia MIT
└── .gitignore           # Archivos ignorados por Git
```

---

## 🚀 Uso

### Método 1: Ejecutar con RUN_ME.bat (Más Fácil)

1. **Navega** a la carpeta donde descargaste los archivos
2. **Doble clic** en `RUN_ME.bat`
3. Si aparece Windows SmartScreen, haz clic en **"Más información"** → **"Ejecutar de todas formas"**

### Método 2: Ejecutar el Script Directamente

**Opción A**: Clic derecho en `InternetHealth.ps1` → **"Ejecutar con PowerShell"**

**Opción B**: Desde PowerShell:

```powershell
powershell -ExecutionPolicy Bypass -File ".\InternetHealth.ps1"
```

### Método 3: Desde PowerShell (Si ya modificaste la Execution Policy)

```powershell
.\InternetHealth.ps1
```

---

## 📊 Interpretando los Resultados

### Estados de Conexión

| Estado        | Color       | Significado                                  |
| ------------- | ----------- | -------------------------------------------- |
| **OK**        | 🟢 Verde    | Conexión estable, < 2% pérdida               |
| **Degradado** | 🟡 Amarillo | Conexión con problemas leves, 2-10% pérdida  |
| **Problemas** | 🔴 Rojo     | Conexión severamente afectada, > 10% pérdida |

### Paneles de la Interfaz

#### 1. **Conexión al Router** (Panel Izquierdo)

- Muestra la calidad de tu conexión **WiFi o Ethernet** al router
- Si este panel marca problemas → el problema es **tu red local**

#### 2. **Conexión a Internet** (Panel Derecho)

- Muestra la calidad de tu conexión a **Internet**
- Si este panel marca problemas **PERO el router está OK** → el problema es **tu proveedor de Internet**

#### 3. **Diagnóstico y Recomendaciones** (Panel Central)

- Análisis inteligente del problema
- Pasos específicos para solucionar el problema
- Cambia de color según la severidad

#### 4. **Registro** (Panel Inferior)

- Historial de todos los eventos
- Botones para limpiar, copiar o exportar el log

---

## ⚙️ Configuración

### Opción 1: Usar config.json (Recomendado)

El proyecto incluye un archivo `config.json` que te permite personalizar la configuración sin tocar el código:

```json
{
  "monitoring": {
    "sampleWindow": 30,
    "pingIntervalMs": 1500,
    "pingTimeoutMs": 1200
  },
  "thresholds": {
    "router": { "highLatencyMs": 30 },
    "internet": { "highLatencyMs": 150 }
  },
  "targets": {
    "internet": ["8.8.8.8", "8.8.4.4"]
  },
  "ui": {
    "windowTitle": "Internet Health Monitor",
    "logMaxLines": 500,
    "logAllPings": true
  }
}
```

Simplemente edita `config.json` y ejecuta la aplicación. Los cambios se aplicarán automáticamente.

### Opción 2: Editar el Script Directamente

También puedes personalizar el comportamiento editando las variables al inicio de `InternetHealth.ps1`:

```powershell
# Configurable thresholds (tune as you like):
$SampleWindow            = 30      # Ventana de muestras recientes
$PingIntervalMs          = 1500    # Intervalo entre pings (ms)
$HighLatencyMsRouter     = 30      # Umbral de latencia alta para router (ms)
$HighLatencyMsInternet   = 150     # Umbral de latencia alta para Internet (ms)
$PingTimeoutMs           = 1200    # Timeout por ping (ms)
$InternetTargets         = @("8.8.8.8","8.8.4.4")  # Servidores DNS para probar Internet
$LogMaxLines             = 500     # Máximo de líneas en el log
$LogAllPings             = $true   # Registrar todos los pings (true) o solo problemas (false)
```

### Targets de Internet Recomendados

Por defecto, el script usa Google DNS (8.8.8.8 y 8.8.4.4). Puedes cambiarlos a:

```powershell
# Cloudflare DNS
$InternetTargets = @("1.1.1.1", "1.0.0.1")

# OpenDNS
$InternetTargets = @("208.67.222.222", "208.67.220.220")

# Quad9 DNS
$InternetTargets = @("9.9.9.9", "149.112.112.112")
```

---

## 🔄 Sistema de Actualización

Internet Health Monitor verifica automáticamente si hay nuevas versiones disponibles.

### **Verificación Automática**

- Al iniciar la app (después de 3 segundos)
- En segundo plano (no interrumpe el monitoreo)
- Te notifica si hay una actualización

### **Verificación Manual**

- Haz clic en el botón **🔄** en el header de la aplicación
- La versión actual siempre está visible

### **Desactivar Verificación Automática**

Edita `config.json`:

```json
{
  "advanced": {
    "checkUpdatesOnStartup": false
  }
}
```

Ver [UPDATE_SYSTEM.md](UPDATE_SYSTEM.md) para más detalles.

---

## 🔧 Solución de Problemas

### Error: "No se puede cargar el archivo... porque la ejecución de scripts está deshabilitada"

**Solución 1**: Usa `RUN_ME.bat` en lugar de ejecutar el script directamente

**Solución 2**: Cambia la política de ejecución temporalmente:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
.\InternetHealth.ps1
```

### Error: "Failed to load required .NET assemblies"

**Causa**: Tu sistema no tiene .NET Framework 4.7.2 o superior

**Solución**: Actualiza Windows Update o descarga .NET Framework desde:
https://dotnet.microsoft.com/download/dotnet-framework

### El script no detecta mi router correctamente

**Solución**: Edita la línea donde se define el gateway:

```powershell
$script:defaultGw = "192.168.1.1"  # Cambia a la IP de tu router
```

### La ventana se cierra inmediatamente al ejecutar

**Causa**: Probablemente hay un error de sintaxis o incompatibilidad

**Solución**: Ejecuta desde PowerShell directamente para ver el error:

```powershell
powershell -ExecutionPolicy Bypass -NoExit -File ".\InternetHealth.ps1"
```

### Windows SmartScreen bloquea la ejecución

**Solución**: Haz clic en **"Más información"** → **"Ejecutar de todas formas"**

Esto es normal para scripts sin firma digital. El código es open source y puedes revisarlo.

---

## 📤 Exportar Reportes

1. Deja la aplicación corriendo durante el problema (al menos 2-5 minutos)
2. Haz clic en **"Exportar Reporte"**
3. Guarda el archivo `.txt` generado
4. Comparte este reporte con tu proveedor de Internet o soporte técnico

El reporte incluye:

- Configuración de red actual
- Estadísticas de router e Internet
- Diagnóstico completo
- Historial de logs

---

## 🤝 Casos de Uso

### Caso 1: Videollamadas Lentas

**Problema**: "Las videollamadas se cortan o van lentas"

**Diagnóstico**:

1. Ejecuta Internet Health Monitor durante una videollamada
2. Si **Router marca problemas** → Acércate al router o usa cable Ethernet
3. Si **Router OK pero Internet con problemas** → Llama a tu proveedor ISP

### Caso 2: Juegos Online con Lag

**Problema**: "Tengo lag en juegos online"

**Diagnóstico**:

1. Configura umbrales más estrictos:
   ```powershell
   $HighLatencyMsRouter = 15     # Gaming requiere baja latencia
   $HighLatencyMsInternet = 50
   ```
2. Monitorea mientras juegas
3. Exporta el reporte para mostrar al proveedor

### Caso 3: Discutir con el ISP

**Problema**: "El proveedor dice que mi Internet está bien, pero yo tengo problemas"

**Solución**:

1. Deja corriendo Internet Health Monitor por varias horas
2. Exporta el reporte con las estadísticas
3. Envía el reporte como evidencia al proveedor

---

## 🛠️ Para Desarrolladores

### Requisitos para Modificar el Código

- Editor de texto o IDE (VS Code recomendado)
- Conocimientos de PowerShell y XAML/WPF

### Estructura del Código

```powershell
# 1. Variables de configuración (líneas 1-40)
# 2. Validación de requisitos (líneas 41-60)
# 3. Carga de assemblies .NET (líneas 61-80)
# 4. Funciones auxiliares (líneas 81-300)
# 5. Definición de UI en XAML (líneas 301-400)
# 6. Lógica de monitoreo (líneas 401-500)
# 7. Manejadores de eventos (líneas 501-600)
# 8. Inicialización y ejecución (líneas 601-fin)
```

### Extender Funcionalidades

Para agregar nuevas funcionalidades, considera:

- Agregar nuevos controles XAML en la sección UI
- Crear funciones auxiliares para la lógica nueva
- Actualizar el timer loop si necesitas monitoreo periódico

---

## 📋 Historial de Versiones

Ver el archivo [CHANGELOG.md](CHANGELOG.md) para el historial completo de cambios.

### Versión Actual: 1.0.0 (2025-11-24)

Características principales de esta versión:

- ✅ Monitoreo en tiempo real de router e Internet
- ✅ Gráficos de latencia con código de colores
- ✅ Sistema de diagnóstico inteligente
- ✅ Exportación de reportes
- ✅ Configuración externa (config.json)
- ✅ Interfaz moderna con WPF

## 📋 Roadmap (Futuras Funcionalidades)

Ver [CHANGELOG.md](CHANGELOG.md) para la lista completa de funcionalidades planificadas:

- [ ] Convertir a ejecutable `.exe` standalone
- [ ] Minimizar a bandeja del sistema (system tray)
- [ ] Notificaciones de Windows cuando se detectan problemas
- [ ] Historial persistente entre sesiones
- [ ] Gráficos de historial de largo plazo (24h, 7d, 30d)
- [ ] Modo "Speedtest" para medir ancho de banda
- [ ] Soporte para múltiples idiomas (i18n)
- [ ] Exportar reportes en formato PDF/HTML

---

## 🐛 Reportar Problemas

Si encuentras un bug o tienes una sugerencia:

1. Verifica que no exista ya un issue similar
2. Crea un nuevo issue con:
   - Descripción clara del problema
   - Pasos para reproducirlo
   - Tu versión de Windows y PowerShell (`$PSVersionTable`)
   - Si es posible, un reporte exportado

---

## 📜 Licencia

Este proyecto está licenciado bajo la **MIT License** - ver el archivo [LICENSE](LICENSE) para más detalles.

**En resumen**: Puedes usar, copiar, modificar, fusionar, publicar, distribuir, sublicenciar y/o vender copias del software libremente.

---

## 👤 Autor

**jcardila**

- 🔗 GitHub: [@jcardila](https://github.com/jcardila)
- 📦 Repositorio: [internet-health-monitor](https://github.com/jcardila/internet-health-monitor)
- Desarrollado para diagnosticar problemas de red de manera simple y efectiva
- Contribuciones y feedback son bienvenidos

---

## 🌟 Agradecimientos

- Comunidad de PowerShell por la documentación
- Usuarios que reportan bugs y sugieren mejoras
- Todos los que comparten esta herramienta

---

## 📸 Screenshots

> **Nota**: Agrega capturas de pantalla de la aplicación aquí cuando estén disponibles

### Pantalla Principal

_[Pendiente: Agregar captura de la interfaz principal]_

### Panel de Diagnóstico

_[Pendiente: Agregar captura del panel de diagnóstico cuando hay un problema]_

### Reporte Exportado

_[Pendiente: Agregar ejemplo de un reporte exportado]_

---

<div align="center">

**¿Te resultó útil esta herramienta? Dale una ⭐ al repositorio!**

</div>
