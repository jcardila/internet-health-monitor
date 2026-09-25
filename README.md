# Monitor de Conexión (Internet Health Monitor) v2

Aplicación de Windows que vive junto al reloj y le dice a cualquier persona, en palabras simples,
**si su conexión está bien para una reunión y, si no, dónde está el problema y qué hacer**.

Tener "todas las rayitas del Wi-Fi" solo significa que el equipo está conectado al punto de acceso.
No dice nada de la calidad real hacia internet. Esta app mide la cadena completa:

```
Tu equipo ── Wi-Fi/Cable ── Router ── Proveedor ── Internet y Microsoft 365
```

Y señala el eslabón que falla: *"Tu señal Wi-Fi está causando problemas"*, *"La salida a internet
de esta red está inestable"*, *"El problema no está en tu equipo ni en tu red"*…

## Qué hace

| | |
|---|---|
| **Bandeja del sistema** | Ícono de color + símbolo (✓ ! ✕) con el estado. Clic: panel con el diagnóstico. |
| **Diagnóstico por eslabón** | Primero evalúa la experiencia real (latencia, variación/jitter, pérdida → calidad estimada para videollamadas, MOS). Solo si es mala busca el culpable. Sin falsas alarmas por routers que responden lento al ping. |
| **Avisos inteligentes** | Solo si el problema persiste. Durante una llamada (micrófono en uso) avisa en ~10 s; fuera de llamadas en ~45 s. Sin repetir, con aviso de recuperación y "No molestar". |
| **Mediciones** | Router, primer salto del proveedor, 3 destinos de internet (Cloudflare, Google, Quad9), conexión real TCP/DNS a Teams y Outlook, señal/banda/velocidad del Wi-Fi, uso de red del equipo, VPN, portal cautivo. Funciona aunque la red bloquee el ping. |
| **Historial local** | Un resumen por minuto en CSV (30 días) y los cambios de estado. Gráficas de 1 h, 6 h, 24 h y 7 días. |
| **Compartir diagnóstico** | Un clic crea un .zip (reporte HTML legible + CSV) en Descargas y copia un resumen listo para pegar en Teams. Nada sale del equipo sin que el usuario lo decida. |
| **Eficiente** | Mide cada 5 s en reposo y cada 1 s en llamada o con problemas. Pausa con el equipo bloqueado o suspendido. La ventana de detalle se destruye al cerrarla. |
| **Lenguaje neutro** | Los mensajes no asumen si la persona está en casa o en una sede: *"Si tienes acceso al router… / si no, avisa a soporte"*. |

## Estructura

```
src/InternetHealth.Core    Lógica sin dependencias de Windows: mediciones, estadísticas, MOS,
                           diagnóstico, histéresis, avisos, historial CSV, reporte HTML.
src/InternetHealth.App     Aplicación WPF (.NET 10): bandeja, panel, ventana de detalle,
                           Wi-Fi (Native Wifi API), detección de llamadas, inicio automático,
                           actualizaciones (Velopack).
tests/                     Pruebas del núcleo (dotnet run, sin dependencias externas).
build/build.ps1            Pruebas + publicación + instalador.
.github/workflows          CI: compila, prueba y publica releases con cada etiqueta v*.
docs/                      Despliegue sin Intune y uso de los datos en Power BI.
legacy/                    Versión 1 (PowerShell), solo como referencia.
```

## Compilar y probar

Requisitos: Windows 10/11 y el SDK de .NET 10 (`winget install Microsoft.DotNet.SDK.10`).

```powershell
dotnet run --project tests/InternetHealth.Core.Tests -c Release   # pruebas
dotnet run --project src/InternetHealth.App                        # abrir la app
dotnet run --project src/InternetHealth.App -- --demo              # modo demostración (simula fallas)
.\build\build.ps1 -Version 2.0.0 -Pack                            # instalador + paquetes de actualización
```

`--demo` recorre escenarios simulados (todo bien → Wi-Fi débil → falla del proveedor → equipo
saturando la red → sin internet). Sirve para capacitar al equipo o tomar capturas.

## Configuración para la organización

`src/InternetHealth.App/defaults.json` va dentro del instalador. Para un equipo o una sede
específica también se lee `C:\ProgramData\InternetHealthMonitor\defaults.json`. Lo más útil de
configurar:

- `supportName`, `supportUrl`, `supportEmail`: a quién acudir (se muestra en los pasos).
- `cloudTargets`: servicios a probar, por ejemplo agregar el servidor de SAP o de Magento.
- `updateFeedUrl`: dónde se publican las actualizaciones (ver [docs/DESPLIEGUE.md](docs/DESPLIEGUE.md)).

## Datos y privacidad

Todo queda en `%LOCALAPPDATA%\InternetHealthMonitorData`: el historial por minuto, los eventos, las
preferencias y un log de errores. No hay telemetría. El formato de los CSV está documentado en
[docs/DATOS-Y-POWERBI.md](docs/DATOS-Y-POWERBI.md), para consolidar los diagnósticos compartidos por
varias sedes y tiendas.

## Licencia

MIT.
