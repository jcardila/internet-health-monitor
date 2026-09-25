# Changelog

Formato basado en [Keep a Changelog](https://keepachangelog.com/es-ES/1.0.0/) y
[versionado semántico](https://semver.org/lang/es/).

## [2.0.0] - 2026-09

Reescritura completa en .NET 10 (WPF). La versión 1 (PowerShell) queda en `legacy/`.

### Nuevo
- Ícono en la bandeja del sistema (globo del color de la barra de tareas con insignia de estado, al estilo de OneDrive o Teams) y panel rápido.
- Texto "Mejorando… confirmando que la conexión se mantenga estable" mientras las cifras ya mejoraron pero el estado aún no cambia.
- Modo demostración siempre visible: franja azul, "[Demo]" en los avisos y "DEMO" en el texto del ícono.
- Cadena de conexión (equipo → Wi-Fi/cable → router → proveedor → internet) con el eslabón culpable resaltado.
- Calidad estimada para videollamadas (MOS) con latencia, variación (jitter) y pérdida.
- Diagnóstico por eslabón: primero se evalúa la experiencia de extremo a extremo y solo si es mala se busca el culpable.
- Nuevas mediciones: primer salto del proveedor, conexión real (DNS + TCP 443) a Teams y Outlook, señal/banda/velocidad del Wi-Fi, uso de red del equipo, VPN y portal cautivo.
- Funciona en redes que bloquean el ping (usa conexiones TCP).
- Avisos de Windows solo cuando el problema persiste, más rápidos durante llamadas (micrófono en uso), con aviso de recuperación y "No molestar".
- Historial local por minuto (30 días) con gráficas de 1 h / 6 h / 24 h / 7 días y registro de cambios de estado.
- "Compartir diagnóstico": .zip con reporte HTML + CSV y resumen copiado para Teams.
- Formato de datos estable para consolidar varias sedes en Power BI.
- Tema claro/oscuro automático, textos en español neutro, accesible (estado con símbolo, no solo color).
- Instalador por usuario sin administrador, inicio con Windows, instancia única y actualizaciones automáticas (Velopack).
- Pruebas automáticas del núcleo y CI en GitHub Actions.

### Corregido (respecto a v1)
- La interfaz ya no se congela: las mediciones corren en segundo plano.
- No hay falsas alarmas por un solo ping perdido ni por routers que responden lento al ping.
- Se detectan los cambios de red, VPN y la reanudación después de suspender.
- La versión del ejecutable se reporta bien (antes quedaba fija en 1.0.0).
- La búsqueda de actualizaciones ya no bloquea la app ni muestra ventanas emergentes.
- Textos que salían cortados en la cadena: "Aquí falla" y "Internet".
- Sin conexión, la latencia muestra "sin respuesta" en vez de un promedio viejo.

## [1.0.0] - 2025-11-24
Versión inicial en PowerShell + WPF (ver `legacy/`).
