# Versión para Mac

La versión para Mac hace lo mismo que la de Windows y usa **el mismo núcleo** (`InternetHealth.Core`):
los mismos umbrales, el mismo diagnóstico por eslabones, los mismos textos, el mismo historial CSV
(se consolida en Power BI junto con los equipos Windows) y el mismo "Compartir diagnóstico".
Lo que cambia es la interfaz y la forma de leer los datos del sistema.

| | Windows | Mac |
|---|---|---|
| Dónde vive | Junto al reloj (bandeja) | Barra de menús, arriba a la derecha |
| Clic en el ícono | Abre el panel | Abre un menú: la primera línea dice el estado en palabras; "Abrir panel" muestra el panel |
| Interfaz | WPF | Avalonia (se ve y se comporta igual: panel, cadena, 5 pestañas) |
| Wi-Fi | Native Wifi API | CoreWLAN (el mismo framework que usa macOS) |
| Nombre de la red (SSID) | Requiere permiso de ubicación (Windows 11 24H2) | Requiere permiso de ubicación (macOS 14 o superior) |
| Llamada en curso | Registro de privacidad del micrófono | Estado del micrófono en CoreAudio (lo mismo que el punto naranja de macOS). No escucha audio |
| Inicio automático | Registro de Windows | LaunchAgent en `~/Library/LaunchAgents` |
| Datos locales | `%LOCALAPPDATA%\InternetHealthMonitorData` | `~/Library/Application Support/InternetHealthMonitorData` |
| Configuración de la organización | `%ProgramData%\InternetHealthMonitor\defaults.json` | `/Library/Application Support/InternetHealthMonitor/defaults.json` |
| Actualizaciones | Velopack desde GitHub Releases (`releases.win.json`) | Velopack desde el mismo release (`releases.osx.json`; Intel: `releases.osx-x64.json`) |

Requisitos: macOS 14 (Sonoma) o superior. Hay dos paquetes: **Apple Silicon** (M1 o superior, la
gran mayoría de los Mac actuales) e **Intel**. Para saber cuál tiene un equipo: menú Apple →
Acerca de este Mac → "Chip" (Apple M…) o "Procesador" (Intel).

## Instalación (sin firma de Apple)

Como la app no está firmada con un certificado de Apple (misma decisión que en Windows), macOS
la bloquea la primera vez. Pasos para la persona:

1. Descargar `InternetHealthMonitor-osx-Setup.pkg` (Apple Silicon) o
   `InternetHealthMonitor-osx-x64-Setup.pkg` (Intel) desde la página del release.
2. Abrirlo. macOS dirá que no puede verificar el desarrollador: pulsar **OK** (no "Mover a la papelera").
3. Ir a **Ajustes del Sistema → Privacidad y seguridad**, bajar hasta "Seguridad" y pulsar
   **Abrir de todas formas**. Confirmar con la contraseña o Touch ID del Mac.
4. Seguir el instalador. La app se abre sola y aparece el globo en la barra de menús.
5. La primera vez, macOS avisa "Se agregó un ítem de inicio de sesión": es el inicio automático.

Opcional, para ver el **nombre de la red Wi-Fi**: en el panel pulsar "Permitir" (o en la ventana
de detalle, Ajustes → "Permiso de ubicación") y aceptar. La app no usa ni guarda la ubicación.

## Si el globo no aparece en la barra de menús

- Los MacBook con muesca (notch) esconden los íconos que no caben: si hay muchos, los de la
  izquierda quedan ocultos detrás de la muesca sin ningún aviso. Cierra otra app de la barra o usa
  una app para organizar íconos. Mientras tanto, abrir "Monitor de Conexión" desde Aplicaciones o
  Spotlight muestra la ventana (la app ya en ejecución se muestra en lugar de abrirse otra vez).
- macOS 26: Ajustes del Sistema → Barra de menús → "Permitir en la barra de menús" debe estar
  activado para "Monitor de Conexion".

## Limitaciones conocidas por no tener firma de Apple

- **Avisos**: macOS 26 no deja que una app sin firma de Apple use el Centro de notificaciones
  ("Notifications are not allowed for this application"). La app lo detecta y usa AppleScript
  como respaldo: el aviso sale a nombre de **Editor de Scripts**, y solo se ve si
  Ajustes del Sistema → Notificaciones → Editor de Scripts está permitido. El registro técnico
  (pestaña Avanzado) dice qué método se está usando.
- El paso de "Abrir de todas formas" en la instalación.

Ambas se resuelven con una cuenta de Apple Developer (99 USD al año): certificado Developer ID
para firmar y notarizar. `build/build-mac.sh` y Velopack ya lo admiten (`--signAppIdentity`,
`--signInstallIdentity`, `--notaryProfile` de `vpk pack`).

## Compilar

Requisitos: un Mac con el SDK de .NET 10 (`brew install dotnet`).

```bash
dotnet run --project tests/InternetHealth.Core.Tests -c Release          # pruebas del núcleo
dotnet run --project src/InternetHealth.Mac -- --demo                    # abrir en modo demostración
./build/build-mac.sh                                                     # .app en artifacts/mac/arm64/
./build/build-mac.sh --version 2.1.0 --pack                              # + instalador .pkg (Velopack)
./build/build-mac.sh --version 2.1.0 --arch x64 --pack                   # para Mac con Intel
```

Con `dotnet run` (sin paquete .app) la app funciona, pero macOS no la reconoce como app: los
avisos usan AppleScript y el permiso de ubicación no se puede pedir. Para probar esas partes,
usa el `.app` de `artifacts/mac/`. Si la carpeta del proyecto está en OneDrive, el script limpia
los atributos extendidos que OneDrive agrega antes de firmar (si no, `codesign` falla).

El CI (`.github/workflows/build.yml`) compila la app de Mac en cada cambio y, con una etiqueta
`v*`, arma los instaladores de las dos arquitecturas y los agrega al mismo borrador del release
de Windows (`vpk upload github --merge`).

## Consumo

Medido en un MacBook con Apple Silicon (macOS 26): ~0,4 % de CPU en reposo; ~68 MB de memoria si
no se ha abierto ninguna ventana y ~100 MB después de abrir y cerrar la ventana de detalle. Es más
que en Windows (Avalonia y macOS pesan más que WPF). Ya se aplicaron: gen0 del GC de 4 MB,
ConserveMemory y dibujo por CPU (sin memoria gráfica retenida).

## Detalles técnicos que ya nos pasaron

- **Ping con TTL**: en macOS, el `Ping` de .NET informa el destino como origen de un
  "TTL expirado", no el salto que respondió. El motor habría tomado el router como proveedor.
  `MacPinger` usa un socket ICMP propio (permitido sin administrador en macOS) para los pings con
  TTL; los pings normales siguen con `SystemPinger`.
- **VPN**: en Mac todas las VPN usan interfaces `utun`, pero macOS también crea varias `utun`
  propias (iCloud, etc.). Solo cuenta como VPN una `utun`/`ipsec`/`ppp` con dirección IPv4.
- **Ícono de la barra de menús**: en macOS 26 la barra es transparente y su texto es claro u
  oscuro según el fondo de pantalla, no según el modo claro/oscuro. El color del globo se toma de
  la apariencia real de la barra (la ventana del ítem de estado) y se revisa cada 5 s.
- **Pestañas y accesibilidad**: las pestañas de Avalonia no responden a la acción de
  accesibilidad "presionar" (herramientas de control remoto en segundo plano); con clics reales sí.
- **Uso de red del equipo**: `NetworkInterface.GetIPStatistics` funciona en Mac. Para probarlo,
  no uses descargas de más de ~100 MB del servidor de pruebas de Cloudflare: las rechaza y parece
  que el medidor no funciona.
