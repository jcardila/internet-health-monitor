# CLAUDE.md — Monitor de Conexión (Internet Health Monitor)

Guía para trabajar en este repositorio. Léela completa antes de cambiar código.

## Qué es y para quién

App de Windows (y, desde la 2.1, de Mac) que vive en la bandeja del sistema (en Mac, la barra de menús) y le dice a cualquier persona de Grupo Ardisa
(~400 personas, muchas sin conocimientos técnicos) **si su conexión está bien para una reunión y,
si no, dónde está el problema y qué hacer**. Nace de un problema real: la gente cree que "tener
todas las rayitas del Wi-Fi" significa que su internet está bien.

- Usuarios en casa, en oficinas y en tiendas. Algunos se mueven entre casa y sedes.
  **La app NO intenta detectar dónde está la persona**: los mensajes son neutros ("Si tienes acceso
  al router… / si no, avisa a soporte TI").
- Dueño del producto: Juan (jcardila). Perfil de negocio, ERP y BI, no de ingeniería de software:
  explica las decisiones técnicas en lenguaje claro.
- Idioma: **todo en español** (interfaz, mensajes, comentarios, documentación). Cultura `es-CO`.

## Restricciones del negocio (no cambiar sin preguntar)

- **Sin Intune**: las licencias de M365 son Basic. La distribución es por instalador por usuario
  (Velopack), sin permisos de administrador, con actualizaciones automáticas desde **GitHub
  Releases** (`updateFeedUrl` en `defaults.json`). El repositorio es **público**: nada de
  secretos, datos de empleados ni configuraciones internas sensibles en el código.
- **Sin firma de código** por ahora (decisión de Juan). El instalador muestra "Windows protegió su
  PC" y hay que pulsar *Más información → Ejecutar de todas formas*.
- **Datos solo locales**: sin telemetría ni envío a servidores. El usuario decide cuándo compartir
  con el botón "Compartir diagnóstico" (.zip con reporte HTML + CSV + resumen copiado para Teams).
- El formato de los CSV es **estable y común a todos los equipos**, pensado para consolidar sedes
  y tiendas en Power BI (`docs/DATOS-Y-POWERBI.md`). Si cambias columnas, agrégalas al final y
  documéntalas; nunca renombres ni elimines columnas.
- Uso bajo de recursos: es un proceso residente. Meta: < 30–50 MB de RAM y ~0 % de CPU en reposo.

## Estructura

```
src/InternetHealth.Core    net10.0, SIN dependencias de Windows ni NuGet. Toda la lógica:
  Model/                   Health (orden = gravedad), Segment, DiagnosisCode…
  Stats/                   SampleBuffer (buffer circular thread-safe), LinkStats, CallQuality (MOS)
  Network/                 Abstracciones (IPinger, ITcpProber, INetworkContextProvider…) e
                           implementaciones multiplataforma (SystemPinger, SystemTcpProber…)
  Diagnosis/               DiagnosisEngine, Thresholds, Messages (textos), StatusTracker
                           (histéresis), NotificationPolicy
  Monitoring/              MonitorEngine (bucle en segundo plano), MonitorSnapshot, LiveLog
  History/                 HistoryStore (CSV por minuto + eventos.csv), MinuteRow, Csv
  Export/                  DiagnosticExporter (.zip), ReportBuilder (HTML), SummaryText
  Settings/                AppSettings (capas defaults.json), UserPreferences, AppPaths,
                           UpdateSchedule (cuándo buscar actualizaciones)
src/InternetHealth.Presentation  net10.0, sin interfaz: ViewModels (Dashboard, History, Settings,
                           MainWindow) y DemoNetwork, compartidos por Windows y Mac. El ícono de cada
                           eslabón es una clave ("Icon.Wifi") que cada app resuelve en sus recursos.
src/InternetHealth.Mac     net10.0 (osx-arm64/x64), Avalonia 11.3 + Velopack. Ver docs/MAC.md.
  Services/                MacHost (equivale a AppHost), MenuBarIcon, MacPinger (TTL por socket ICMP),
                           MacNetworkContextProvider, MicrophoneCallDetector (CoreAudio), Notifier,
                           LaunchAgent, SessionMonitor, LocationPermission, UpdateService
  Interop/                 ObjC (objc_msgSend, bloques), MacNative (CoreAudio, CoreGraphics), CoreWlanClient
  Views/ Controls/         PanelWindow, MainWindow, ChainView, Sparkline, HistoryChart, StatusBadge
src/InternetHealth.App     net10.0-windows, WPF + NotifyIcon de WinForms, Velopack.
  Program.cs               Main propio: Velopack → instancia única → cultura → App
  Services/                AppHost (orquesta todo), TrayIcon(+Renderer), ThemeManager,
                           WindowsNetworkContextProvider, MicrophoneCallDetector,
                           StartupManager, SingleInstance, UpdateService, DemoNetwork
  Interop/                 P/Invoke (iphlpapi, wlanapi, dwmapi, user32), WlanClient
  ViewModels/ Views/ Controls/ Themes/
tests/InternetHealth.Core.Tests   Mini framework propio ([Test]), sin xUnit (ver "Aprendizajes")
build/build.ps1            Pruebas + publish autocontenido + instalador Velopack (-Pack)
build/build-mac.sh         Mac: pruebas + publish + .app firmado ad hoc (+ .pkg de Velopack con --pack)
build/mac/                 Info.plist (LSUIElement, textos de permiso de ubicación) y AppIcon.icns
build/compilar-local.cmd   Doble clic: instala el SDK si falta, prueba, compila y abre --demo
.github/workflows/build.yml  CI: pruebas y compilación en Windows y Mac en cada push; con una etiqueta v*
                           arma los instaladores y los sube al mismo borrador de GitHub Releases
                           (el job de Mac corre después y usa `vpk upload github --merge`)
docs/                      DESPLIEGUE.md (sin Intune), MAC.md y DATOS-Y-POWERBI.md (esquema CSV)
legacy/                    v1 en PowerShell, solo como referencia. No se mantiene.
```

## Comandos

```powershell
dotnet run --project tests/InternetHealth.Core.Tests -c Release   # pruebas (código de salida ≠ 0 si falla)
dotnet run --project tests/InternetHealth.Core.Tests -- Diagnosis # filtra pruebas por nombre
dotnet build src/InternetHealth.App -c Debug
dotnet run --project src/InternetHealth.App -- --demo             # red simulada
.\build\build.ps1 -Version 2.0.1 -Pack                            # instalador local en artifacts\releases
```

En Mac (`brew install dotnet`):

```bash
dotnet run --project src/InternetHealth.Mac -- --demo              # sin .app: avisos por AppleScript, sin permiso de ubicación
./build/build-mac.sh --skip-tests                                  # .app en artifacts/mac/arm64/
./build/build-mac.sh --version 2.1.0 --pack [--arch x64]           # + .pkg en artifacts/releases-mac/<arch>
dotnet build src/InternetHealth.App                                # WPF también compila en Mac (EnableWindowsTargeting)
```

## Publicar una versión (flujo acordado)

1. Cambios en `dev` → pruebas en verde → llevar a `main` (hasta ahora, avance rápido:
   `git branch -f dev main` después de cada commit en `main` para mantenerlas iguales).
2. Subir la versión en `CHANGELOG.md`. Versionado semántico: 2.0.x arreglos, 2.x.0 funciones.
3. `git tag v2.0.1` y `git push origin v2.0.1`. **Esperar a que el CI de `main` esté en verde
   antes de etiquetar** (la primera vez se etiquetó un commit con una prueba rota).
4. CI: pruebas → `build.ps1 -Pack -GithubRepo …` (descarga el release anterior para crear el
   delta) → `vpk upload github` como borrador, con nombre "v2.1.1 - Monitor de Conexión" (la
   etiqueta al principio; el job de Mac usa el mismo nombre con `--merge`).
5. Juan revisa el borrador y pulsa *Publish release*. Solo entonces llega a los equipos (en ≤ 24 h).
   Los borradores y pre-releases no son "latest" y nunca llegan a nadie.
6. Verificación rápida tras publicar:
   `Invoke-RestMethod https://github.com/jcardila/internet-health-monitor/releases/latest/download/releases.win.json`
   debe mostrar la versión nueva.

Nunca reutilices un número de versión ya publicado: Velopack no actualiza a la "misma" versión.
Si un release sale mal, se publica uno nuevo (2.0.2), no se reemplaza.

Argumentos de la app: `--background` (inicio automático, sin ventanas), `--demo` (red simulada:
todo bien → Wi-Fi débil → proveedor → equipo saturado → sin internet; además finge una llamada de
Teams la mitad del tiempo; los datos van a `%TEMP%\InternetHealthMonitorDemo`).

La app WPF solo **corre** en Windows, pero **compila** en Mac con NuGet disponible (sirve para
verificar cambios en Presentation). En la nube (Linux sin NuGet) solo compilan Core y Tests.

## Principios de diseño del diagnóstico (lo más importante)

1. **Primero la experiencia de extremo a extremo; después el culpable.** Se evalúa internet
   (latencia, jitter y pérdida, más el MOS estimado). Solo si está Fair/Poor/Down se busca el
   eslabón culpable. Si internet está Good, las pérdidas o demoras de ping en router o proveedor
   se ignoran: los routers dan baja prioridad al ICMP. Esto eliminó la falsa alarma principal de
   la v1 ("router con problemas" por un ping lento).
2. **Cadena de 5 eslabones**: Tu equipo → Wi-Fi/Cable → Router → Proveedor → Internet.
   El proveedor es el primer salto público, descubierto con pings con TTL (CGNAT 100.64/10 cuenta
   como proveedor).
3. **Orden para encontrar al culpable** (en `DiagnosisEngine`):
   - Wi-Fi débil **con** pérdida local.
   - Luego la red local (router).
   - Luego el equipo saturando la red (tráfico alto sostenido 8 s).
   - Luego el proveedor.
   - Si todo lo local está bien, el problema es externo.
4. **Redes que bloquean el ping** (comunes en redes corporativas): si todo el ICMP a internet se
   pierde pero el TCP 443 a Microsoft 365 funciona → `IcmpBlocked`. Se usan las mediciones TCP y
   se sube su frecuencia a cada 2 s.
5. **Router que no responde al ping**: solo es "medible" si respondió al menos una vez en esta red.
   Si nunca respondió, queda gris ("normal en algunos equipos") y nunca se le culpa.
6. **Histéresis** (`StatusTracker`):
   - Empeorar exige 8 s (4 s si es Down).
   - Mejorar exige 20 s.
   - Cambiar de causa con la misma gravedad exige 10 s.
7. **Avisos** (`NotificationPolicy`):
   - Solo si el problema persiste: 10 s en llamada, 45 s fuera de llamadas.
   - Fuera de llamada, solo para gravedad Poor o peor. En llamada también para Fair, si el
     culpable es Wi-Fi, el equipo o el router (el usuario puede hacer algo).
   - No se repite el mismo aviso antes de 30 min. Hay aviso de recuperación y "No molestar".
8. **Umbrales** (`Thresholds`): se basan en la guía de red de Teams (RTT < 100 ms, jitter < 30 ms,
   pérdida < 1 %), con margen por el ruido del ping (con 30 muestras, 1 pérdida = 3,3 %, y eso
   no debe ser alarma). No los endurezcas sin pruebas: el objetivo #1 es cero falsas alarmas.
9. **Muestreo adaptativo**:
   - En reposo, cada 5 s.
   - Cada 1 s en llamada, con problemas, con la ventana abierta o durante 30 s después de un
     cambio de red.
   - En pausa con la sesión bloqueada (salvo en llamada) o el equipo suspendido.
   - La detección de llamadas lee el registro de privacidad del micrófono
     (CapabilityAccessManager\ConsentStore\microphone: `LastUsedTimeStop == 0` = en uso).
     No escucha audio.

## Convenciones de interfaz y textos

- Textos en `Core/Diagnosis/Messages.cs`:
  - Segunda persona, frases cortas, sin jerga, sin culpar al usuario.
  - **Neutros casa/sede**: nunca "tu router" a secas.
  - El texto de bandeja tiene máximo 60 caracteres; hay una prueba que lo verifica.
- **El estado nunca depende solo del color**: siempre va con símbolo (✓ ! ✕ …) y texto.
- Paleta de estados (skill de dataviz): good `#0CA30C`, fair `#FAB219`, poor `#EC835A`,
  down `#D03B3B`, unknown `#898781`. Serie de gráficas: `#2A78D6` (claro) / `#3987E5` (oscuro).
- Tema claro/oscuro: `ThemeManager` inserta `Themes/Light.xaml` o `Dark.xaml` (mismas llaves) y
  escucha los cambios de Windows. `ThemeMode=System` (Fluent) se asigna **en código** en
  `Program.cs`, no en XAML.
- Ícono de bandeja: globo monocromo del color de la **barra de tareas** (`SystemUsesLightTheme`,
  que es distinto del tema de las apps) + insignia de estado recortada en la esquina, al estilo
  de OneDrive o Teams. Se dibuja a 4× y se reduce.
- Modo demo siempre visible: franja azul, "[Demo]" en los avisos y "DEMO" en el tooltip.
- Las ventanas de detalle se destruyen al cerrarse (se hace un GC y la memoria vuelve al mínimo).
  El panel rápido (flyout) se reutiliza.

## Aprendizajes y trampas conocidas (ya nos pasaron)

**WPF / .NET**
- Con `UseWPF=true` el SDK **quita los usings implícitos `System.IO` y `System.Net.Http`**:
  agrega `using System.IO;` a mano.
- El csproj quita `System.Windows.Forms` y `System.Drawing` de los usings globales para evitar
  ambigüedades (Application, Color, Brush…). Usa esos tipos con su namespace completo o con un
  `using` local.
- **No uses arreglos (`double?[]`) como tipo de DependencyProperty** si se asignan desde XAML
  dentro de una plantilla: falla con `MC4102 PropertyArrayStart`. Usa `IReadOnlyList<T>`.
- `App.xaml` se compila como `Page` (no como ApplicationDefinition) porque `Program.Main` debe
  ejecutar Velopack e instancia única antes de crear la app. Es válido: `InitializeComponent`
  se genera igual.
- Asignar `ThemeMode` en XAML junto con `Application.Resources` puede descartar el diccionario
  Fluent: se asigna en código.
- `Pen.Freeze()` sobre pinceles de recursos: verifica `CanFreeze` antes.
- Los eventos del motor llegan en otro hilo: todo lo de WPF y NotifyIcon se toca con
  `Dispatcher.BeginInvoke`. El refresco de la UI se descarta si ya hay uno en cola.
- `Velopack.ApplyUpdatesAndRestart` termina el proceso con `Environment.Exit` y **no** dispara
  `Application.Exit`. Antes de llamarlo, vacía el historial y desecha el motor y el ícono de la
  bandeja; si no, queda un ícono fantasma.
- Velopack está fijado en **1.2.158**, igual que la herramienta `vpk` (`build.ps1 -VpkVersion`).
  Antes estaba en `0.0.*`, que resolvía a la 0.0.1298 (muy vieja: la serie actual es 1.x). Si
  subes una, sube la otra. `vpk pack` necesita `--runtime win-x64` o marca el paquete como x86.
- **Horas en los CSV**: al leer, reconstruye el instante con la columna UTC (`minute_utc`,
  `time_utc`), no con la zona horaria del equipo que lee. Leer la hora local con la zona del
  lector corría las filas 5 h en GitHub Actions (servidores en UTC) y rompió la prueba de
  historial. Las pruebas que dependan de la zona horaria deben usar un offset distinto al de
  Colombia (p. ej. +3) para fallar también en este PC.

**Mac (Avalonia + macOS)**
- `Ping` de .NET en macOS informa el **destino** como origen de un TtlExpired: el primer salto
  "del proveedor" salía igual al router. `MacPinger` usa un socket ICMP DGRAM propio (sin admin)
  para los pings con TTL. macOS entrega la cabecera IP con el ICMP.
- Sin firma de Apple, macOS 26 rechaza UNUserNotificationCenter ("Notifications are not allowed
  for this application"), aunque la app esté en ~/Applications. `Notifier` cae a AppleScript
  (sale como Editor de Scripts). Se ve en el registro técnico. Se arregla con Developer ID.
- La barra de menús de macOS 26 cambia de claro a oscuro según el fondo de pantalla, no según el
  tema: el color del globo se lee de la ventana del NSStatusItem (`MenuBarAppearance`), cada 5 s.
- Las `utun` existen siempre (iCloud…): VPN solo si una utun/ipsec/ppp tiene IPv4.
- La carpeta del repo está en OneDrive: agrega atributos extendidos que `codesign` rechaza →
  `xattr -cr` antes de firmar. Pero **no** borres xattr después de firmar: la firma de las .dll de
  Contents/MacOS vive en atributos extendidos (copiar con `ditto`, no con `cp` + `xattr -c`).
- Memoria: en chips Apple .NET usa un gen0 enorme; `System.GC.Gen0Size`=4 MB y ConserveMemory=5
  (csproj de Mac) más el dibujo por CPU (`AvaloniaNativeRenderingMode.Software`, Metal retenía
  ~35 MB) dejan ~68 MB en reposo sin ventanas y ~100 MB tras abrir/cerrar la ventana (antes 160).
- OneDrive (File Provider) a veces bloquea escribir .dll en bin/ ("Access denied" persistente):
  build-mac.sh compila en una copia en $TMPDIR y trae el .app con `ditto`.
- `vpk pack` en Mac: `--packTitle` con "ó" rompe pkgbuild; la carpeta es "Monitor de Conexion.app"
  y el nombre visible sale de es.lproj/InfoPlist.strings. `--bundleId` y `--plist` no se pueden juntar.
- Si el globo "desaparece": en MacBook con muesca, los íconos que no caben quedan ocultos (la ventana
  del ítem mide 0×0). No es un error de la app.
- Para ver la app con computer-use se concede por bundle id `com.jcardila.internethealthmonitor`
  (solo existe con el .app). Las pestañas de Avalonia no reaccionan a AXPress: usar clics reales.
- No pruebes el medidor de tráfico con descargas >100 MB de speed.cloudflare.com: las rechaza.
- `--demo` con el .app: `open -n ".../Monitor de Conexion.app" --args --demo`.

**Actualizaciones (Velopack + GitHub Releases)**
- La **API** de GitHub (`api.github.com`, la que usa `GithubSource`) limita a 60 consultas/h por IP
  sin token. Las **descargas directas** (`github.com/…/releases/latest/download/archivo`) no tienen
  ese límite (verificado: la API responde `X-RateLimit-Limit: 60`, la descarga no). Por eso
  `UpdateService` usa `new UpdateManager(url)` (SimpleWebSource) con la URL de descarga directa.
  Nunca metas un token de GitHub en la app.
- `latest/download` sirve los archivos del último release **publicado**. Si alguien está varias
  versiones atrás, el delta no está ahí y Velopack baja el paquete completo (~70 MB): es normal.
- Solo se reciben actualizaciones en instalaciones hechas con `Setup.exe`
  (`UpdateManager.IsInstalled`); corriendo desde `bin\Debug` o la demo no se busca nada.
- El instalador pesa ~77 MB porque es autocontenido (.NET + WPF no admiten recorte).

**Red y Windows**
- `PingReply.RoundtripTime` vale **0** para `TtlExpired`: mide con Stopwatch en los pings con TTL.
- `WLAN_BSS_LIST.dwTotalSize` incluye los bloques IE después del arreglo: valida
  `total >= 8 + count*360`, no la igualdad. Offsets verificados en x64:
  - `WLAN_CONNECTION_ATTRIBUTES`: la asociación empieza en 520. Dentro de ella: SSID +0,
    BSSID +40, PHY +48, señal +56, rx +60, tx +64.
  - `WLAN_BSS_ENTRY` (360 bytes): BSSID en 40, RSSI en 56, frecuencia en 92.
- En **Windows 11 24H2** la Native Wifi API devuelve `ERROR_ACCESS_DENIED` si no está permitido el
  acceso de apps de escritorio a la ubicación. La app degrada con gracia: usa la velocidad del
  enlace y muestra una pista para activar el permiso.
- **Wi-Fi y el ícono de "ubicación en uso" (Windows 11 24H2)**: verificado leyendo
  `HKCU\...\CapabilityAccessManager\ConsentStore\location\NonPackaged\<ruta del exe>`
  (`LastUsedTimeStart`). Cuentan como uso de ubicación: `WlanQueryInterface` con
  `CURRENT_CONNECTION` (SSID/BSSID) y `WlanGetNetworkBssList`. **No** cuentan: `WlanQueryInterface`
  con RSSI o canal, la velocidad del adaptador y `GetSignalBars` de WinRT. Por eso la señal se lee
  en cada ronda (RSSI → % con `WifiInfo.QualityFromRssi`) y la lectura completa solo al abrir la
  ventana, cada 30 min (`MonitorEngine.WifiDetailsInterval`) o si el proveedor ve otro router,
  adaptador o canal. Antes (≤ 2.1.0) se leía todo cada 5–15 s y el ícono parpadeaba: ~240 usos/h.
  Para medir un cambio: correr el exe de `bin\Debug` con `--background` y contar cambios de esa
  clave del registro.
- Un "parpadeo" de la red (cambio de IPv6/VPN) puede hacer desaparecer el router un instante y
  provocar un reinicio de mediciones: los reinicios no deben forzar lecturas costosas o con permisos.
- La app **no** impide la suspensión: no usa `SetThreadExecutionState` ni solicitudes de energía
  (revisado el 4 oct. tras sospecha de Juan). Para diagnosticar "el portátil ya no entra en
  reposo": `powercfg /requests` (requiere administrador) y los eventos Kernel-Power 506/507 del
  registro System (entrada y salida del reposo moderno, S0).
- `NetworkChange.NetworkAddressChanged` se dispara muy seguido (IPv6, VPN): solo pide releer el
  contexto. El reinicio completo de mediciones ocurre solo si cambian el router o el adaptador.
- La MAC del router (SendARP) identifica la red o sede sin pedir permisos. Es la llave recomendada
  para agrupar en Power BI.

**Entorno de Claude (sesión local en la app de escritorio, lo normal desde el 24 sep.)**
- Se trabaja directo en el PC de Juan: PowerShell 7, `dotnet`, compilación de WPF, pruebas y
  `build.ps1 -Pack` funcionan. `gh` no está instalado.
- `git push` funciona con las credenciales que Juan guardó en Git. Si vuelve a fallar con
  "Invalid username or token", Juan debe ejecutar `git push` en su terminal para iniciar sesión:
  Claude no ingresa credenciales.
- **Antes de `git add -A`, revisa `git status`.** El 4 oct. el índice del PC había quedado con el
  estado de antes de la 2.1.0 (que se commiteó desde el Mac): un `git add -A` habría borrado la app
  de Mac del repositorio. Si aparecen cambios preparados ("D ", "MM") que no hiciste, `git reset`
  (sin `--hard`) realinea el índice con HEAD sin tocar archivos.
- `git commit -F -` con un here-string de PowerShell **no** pasa el mensaje: escribe el mensaje en
  un archivo del scratchpad (UTF-8 sin BOM) y usa `git commit -F archivo`.
- El filtro de seguridad de la herramienta PowerShell a veces bloquea comandos con `.Replace(...)`
  sobre XML o rutas; usa la herramienta Edit para cambios de texto.
- Para ver la app: lanzar `bin\Debug\...\InternetHealthMonitor.exe --demo` y pedir acceso de
  computer-use a `InternetHealthMonitor.exe` (por nombre de app no la encuentra).

**Entorno de Claude (sesiones en la nube vinculadas a este PC)**
- En la nube, NuGet está bloqueado y no hay targeting packs de WPF: solo se compila Core con el
  `dotnet-sdk-10.0` de apt. Para validar la App hay que compilar en el PC.
- La terminal de Windows y el Explorador tienen acceso de "solo clic" en el control de
  escritorio: no se puede escribir en ellos. Por eso existe `build/compilar-local.cmd`, que se
  ejecuta con doble clic y deja el registro en `artifacts/compilacion.log`. Ese registro se lee
  trayendo el archivo al contenedor, no desde capturas de pantalla.
- PowerShell 5.1: las redirecciones escriben UTF-16 por defecto. El script fija
  `$PSDefaultParameterValues['Out-File:Encoding']='utf8'`. Guarda los `.ps1` con BOM UTF-8 si
  tienen tildes.
- `.github/workflows/` es una ruta protegida para las herramientas remotas (nube). En una sesión
  local de la app de escritorio sí se puede escribir.
- En sesiones locales, la computer-use concede la app por su .exe (`InternetHealthMonitor.exe`),
  no por su nombre. Para ver el ícono de bandeja con detalle es mejor dibujarlo a PNG con un
  script (`dotnet run render.cs`) que hacer zoom en capturas.
- El script de compilación cierra la instancia en ejecución (`Stop-Process InternetHealthMonitor`)
  porque la instancia única y el .exe bloqueado impiden recompilar.

## Pruebas

- `tests/` usa un mini framework propio (`TestFramework.cs`) porque NuGet no está disponible en el
  entorno de nube. Para agregar pruebas: métodos públicos con `[Test]`, sync o async, y la clase
  `Assert` propia.
- `Builders.cs` arma escenarios (`Build.Assess(...)`, `Build.Steady(ms, count, lost, wobble)`).
- Cubren estadísticas, MOS, cada código de diagnóstico, histéresis, avisos, el motor con red
  falsa (`FakePinger`…), el historial (también leído en otra zona horaria), la exportación y el
  calendario de actualizaciones (`UpdateScheduleTests`) y cuándo se leen los datos del Wi-Fi que
  usan la ubicación. Son 51 en total.
- El CI de GitHub corre las pruebas en UTC (y con otra cultura regional): una prueba que pasa en el PC puede fallar
  allí si depende de la zona horaria o la cultura.
- Cualquier cambio en `Thresholds`, `DiagnosisEngine` o `Messages` debe venir con una prueba del
  escenario. Sobre todo si evita o causa falsas alarmas.
- En Windows los archivos abiertos quedan bloqueados: cierra los streams y zips antes de borrar
  carpetas temporales en las pruebas.

## Estado actual (24 sep. 2026)

- **v2.0.0 publicada** en GitHub Releases
  (https://github.com/jcardila/internet-health-monitor/releases/tag/v2.0.0), con `Setup.exe`,
  paquete completo y `releases.win.json`. El feed `…/releases/latest/download` responde bien.
  Esta primera versión no tiene delta (no había release anterior).
- `main` y `dev` están iguales y subidos. 49 pruebas en verde, en el PC y en CI.
- Verificado en la demo en el PC de Juan: ícono de bandeja (globo + insignia) en barra clara y
  oscura, franja de demo, "Aquí falla", etiqueta "Internet" y "Mejorando… confirmando".
  No se vio en pantalla (solo compila): "sin respuesta" en la latencia durante la fase sin internet.
- Raíz limpia: las copias de la v1 están solo en `legacy/` (el .zip de la v1 también, ignorado).
- Decisiones tomadas:
  - Feed de actualizaciones: GitHub Releases con descarga directa. `UpdateSchedule` reparte las
    consultas: 2–20 min al azar al iniciar, ~24 h tras un éxito (`UserPreferences.LastUpdateCheck`)
    y espera creciente de 1 h hasta 8 h si falla. Si se descargó en plena llamada, reintenta
    instalar cada 30 min.
  - Sin firma de código por ahora. Azure Artifact Signing con certificado público no admite
    empresas de Colombia (sep. 2026). La opción futura recomendada es un certificado OV con firma
    en la nube (SSL.com eSigner o DigiCert KeyLocker) para firmar desde el CI; `build.ps1` ya
    acepta `-SignParams`.

- **Versión para Mac (29 sep. 2026, sin publicar, 2.1.0)**: probada en el MacBook de Juan (macOS 26,
  Apple Silicon) en demo y en modo real: salto del proveedor correcto, Wi-Fi con SSID (tras permiso
  de ubicación), MAC del router, instalación con el .pkg "solo para mí" sin contraseña, arranque al
  iniciar sesión. Instalada en ~/Applications de Juan. Avisos: por AppleScript (sin firma de Apple).

- **2.1.1 (4 oct. 2026, lista, sin publicar)**: corrige el ícono de ubicación que parpadeaba en
  Windows. Medido en el PC de Juan con la red real: 1 uso de ubicación al iniciar y ninguno en los
  4 minutos siguientes (antes ~16 en ese tiempo). Mac sin cambios de comportamiento.
- Investigado (4 oct.): el portátil de Juan dejó de entrar en reposo desde el reinicio del 30 sep.
  La app no lo impide (ver "Red y Windows"). Su plan Balanced tiene suspensión e hibernación en
  "Nunca" con cargador. Falta que Juan ejecute `powercfg /requests` como administrador.

## Pendientes

**De Juan**
- Instalar la v2.0.0 en modo real en su PC y usarla unos días; anotar falsas alarmas o textos
  confusos.
- Datos de soporte reales en `defaults.json`: `supportName`, `supportUrl` (Zoho Desk) y
  `supportEmail`. Hoy los pasos sugeridos dicen "soporte TI" sin enlace. Requiere una v2.0.1.
- Piloto con 5–10 personas de sedes, casa y tiendas (avisarles del clic extra por no estar
  firmado). Incluir: un equipo con cable, uno con VPN, una red que bloquee el ping y un portátil
  con Windows 10.
- Más adelante: decidir la compra del certificado OV antes de repartir a las ~400 personas.
- Mac: decidir si se paga Apple Developer (99 USD/año) para firmar y notarizar; sin eso los avisos
  salen por AppleScript y la instalación pide "Abrir de todas formas".

**Técnicos (para Claude)**
- Revisar con datos reales: al recuperarse de un Wi-Fi débil, la ventana de mediciones aún guarda
  las pérdidas viejas y durante ~30 s se culpa al router o al proveedor ("cambio de causa" con la
  misma gravedad). Se vio en la demo. Si pasa en redes reales, corregirlo con una prueba del
  escenario (p. ej. no cambiar de culpable mientras el eslabón nuevo solo tiene pérdidas antiguas).
- Probar la primera actualización real: publicar una v2.0.1 y confirmar que un equipo con la
  v2.0.0 instalada se actualiza solo (delta), sin ícono fantasma y sin interrumpir una llamada.
- Mac: probar la instalación real con el .pkg de un release (Velopack), la actualización
  automática a una versión siguiente y la detección de llamadas con una reunión real de Teams.
- Mac: plantilla de Power BI debe tolerar `connection_type`/`adapter` con nombres de Mac ("Wi-Fi (en0)").
- Advertencias de análisis de código sin atender (CA1806, CA1001, CA1725, WFO0003): no bloquean,
  pero conviene limpiarlas algún día.

**Ideas a futuro**
- Botones de acción en los avisos (requiere el SDK de Windows / AppNotification).
- Plantilla de Power BI sobre los CSV compartidos (llave recomendada: MAC del router).
- Canal "piloto" separado del estable (Velopack `--channel`) cuando haya más usuarios.

Commitea solo cuando Juan lo pida.
