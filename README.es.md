# Internet Health Monitor

<div align="center">

![Version](https://img.shields.io/badge/version-1.0.0-blue.svg)
![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-lightgrey.svg)
![PowerShell](https://img.shields.io/badge/PowerShell-5.1%2B-blue.svg)
![License](https://img.shields.io/badge/license-MIT-green.svg)

**Una herramienta visual y amigable para diagnosticar problemas de conexión a Internet en Windows**

[English](README.md) | Español

</div>

---

## 🚀 Inicio Rápido

1. **Descarga** todos los archivos en una carpeta
2. **Doble clic** en `RUN_ME.bat`
3. ¡Listo! La aplicación comenzará a monitorear tu conexión

---

## 📋 ¿Qué hace esta herramienta?

**Internet Health Monitor** te ayuda a responder la pregunta más importante cuando tienes problemas de Internet:

> **"¿El problema es mi WiFi/router o es mi proveedor de Internet?"**

### La aplicación te dirá:

✅ **Si tu problema es el WiFi/Router** (conexión local)

- Te sugerirá acercarte al router
- Te dirá si hay interferencias
- Te indicará si debes reiniciar el router

✅ **Si tu problema es el proveedor de Internet** (ISP)

- Te confirmará que TU conexión está bien
- Te dará pasos para contactar al proveedor
- Te permitirá exportar un reporte como evidencia

---

## 🖥️ ¿Qué necesito?

- **Windows 10 o Windows 11** (actualizado)
- **Nada más!** No requiere instalación ni permisos especiales

---

## 📊 Cómo interpretar los resultados

### Panel Izquierdo: "Conexión al Router"

- 🟢 **OK** = Tu WiFi/cable al router funciona bien
- 🟡 **Degradado** = Hay problemas leves con tu conexión local
- 🔴 **Problemas** = Tu WiFi/cable está fallando

### Panel Derecho: "Conexión a Internet"

- 🟢 **OK** = El Internet del proveedor funciona bien
- 🟡 **Degradado** = Hay problemas leves con el Internet
- 🔴 **Problemas** = El servicio de Internet está fallando

### La Regla de Oro 🎯

**Si el Router marca rojo, arréglalo primero antes de culpar al proveedor**

Esto es porque todo el tráfico pasa por el router. Si el router falla, no puedes saber si el Internet realmente está mal.

---

## 💡 Casos de Uso Comunes

### 1. Videollamadas que se cortan

**Ejecuta la aplicación durante una videollamada**

- Si marca problemas en Router → Acércate al router o usa cable
- Si marca problemas en Internet → Contacta a tu proveedor

### 2. Juegos online con lag

**Monitorea mientras juegas**

- Verás en tiempo real si el problema es local o del proveedor
- Exporta el reporte para enviarlo a soporte técnico

### 3. Discutir con tu proveedor

**Deja corriendo la aplicación por horas**

- Captura evidencia de los problemas
- Haz clic en "Exportar Reporte"
- Envía el reporte al proveedor como prueba

---

## ⚙️ Configuración Básica

Abre `InternetHealth.ps1` con un editor de texto y cambia estas líneas al inicio:

```powershell
$PingIntervalMs = 1500    # Cada cuánto hacer ping (en milisegundos)
$LogAllPings = $true      # true = registrar todo | false = solo problemas
```

---

## 🔧 Problemas Comunes

### "No se puede ejecutar el script"

→ Usa `RUN_ME.bat` en lugar del archivo `.ps1`

### "Windows SmartScreen bloqueó la ejecución"

→ Haz clic en "Más información" → "Ejecutar de todas formas"

### "La ventana se cierra inmediatamente"

→ Asegúrate de tener Windows actualizado con todas las actualizaciones

---

## 📤 Exportar Reportes

1. Deja la aplicación corriendo durante el problema
2. Haz clic en **"Exportar Reporte"**
3. Guarda el archivo
4. Envíalo a tu proveedor o soporte técnico

---

## 📜 Licencia

MIT License - Úsalo libremente para lo que quieras

---

## 🤝 Contribuciones

¿Encontraste un bug? ¿Tienes una idea?

- Reporta issues en GitHub
- Los pull requests son bienvenidos

---

<div align="center">

**¿Te fue útil? Dale una ⭐ al repositorio!**

[⬆ Volver arriba](#internet-health-monitor)

</div>
