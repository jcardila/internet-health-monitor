# CLAUDE.md — Monitor de Conexión (Internet Health Monitor)

Guía para trabajar en este repositorio. Léela completa antes de cambiar código.

## Qué es y para quién

App de Windows que vive en la bandeja del sistema y le dice a cualquier persona de Grupo Ardisa
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
  (Velopack), sin permisos de administrador, con actualizaciones automáticas desde una URL
  (`updateFeedUrl`).
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
  Settings/                AppSettings (capas defaults.json), UserPreferences, AppPaths
src/InternetHealth.App     net10.0-windows, WPF + NotifyIcon de WinForms, Velopack.
  Program.cs               Main propio: Velopack → instancia única → cultura → App
  Services/                AppHost (orquesta todo), TrayIcon(+Renderer), ThemeManager,
                           WindowsNetworkContextProvider, MicrophoneCallDetector,
                           StartupManager, SingleInstance, UpdateService, DemoNetwork
  Interop/                 P/Invoke (iphlpapi, wlanapi, dwmapi, user32), WlanClient
  ViewModels/ Views/ Controls/ Themes/
tests/InternetHealth.Core.Tests   Mini framework propio ([Test]), sin xUnit (ver "Aprendizajes")
build/build.ps1            Pruebas + publish autocontenido + instalador Velopack (-Pack)
build/compilar-local.cmd   Doble clic: instala el SDK si falta, prueba, compila y abre --demo
.github/workflows/build.yml  CI en windows-latest; publica un release con cada etiqueta v*
docs/                      DESPLIEGUE.md (sin Intune) y DATOS-Y-POWERBI.md (esquema CSV)
legacy/                    v1 en PowerShell, solo como referencia. No se mantiene.
```

## Comandos

```powershell
dotnet run --project tests/InternetHealth.Core.Tests -c Release   # pruebas (código de salida ≠ 0 si falla)
dotnet run --project tests/InternetHealth.Core.Tests -- Diagnosis # filtra pruebas por nombre
dotnet build src/InternetHealth.App -c Debug
dotnet run --project src/InternetHealth.App -- --demo             # red simulada
.\build\build.ps1 -Version 2.0.1 -Pack                            # instalador + feed de actualizaciones
```

Argumentos de la app: `--background` (inicio automático, sin ventanas), `--demo` (red simulada:
todo bien → Wi-Fi débil → proveedor → equipo saturado → sin internet; además finge una llamada de
Teams la mitad del tiempo; los datos van a `%TEMP%\InternetHealthMonitorDemo`).

La compilación de WPF **solo funciona en Windows**. En Linux solo compilan y se prueban Core y Tests.

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
- `NetworkChange.NetworkAddressChanged` se dispara muy seguido (IPv6, VPN): solo pide releer el
  contexto. El reinicio completo de mediciones ocurre solo si cambian el router o el adaptador.
- La MAC del router (SendARP) identifica la red o sede sin pedir permisos. Es la llave recomendada
  para agrupar en Power BI.

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
  falsa (`FakePinger`…), el historial y la exportación.
- Cualquier cambio en `Thresholds`, `DiagnosisEngine` o `Messages` debe venir con una prueba del
  escenario. Sobre todo si evita o causa falsas alarmas.
- En Windows los archivos abiertos quedan bloqueados: cierra los streams y zips antes de borrar
  carpetas temporales en las pruebas.

## Estado y pendientes (sep. 2026)

- v2.0.0 compila en Windows y las 43 pruebas pasan. Verificado en la demo en el PC de Juan (24 sep.):
  ícono de bandeja (globo + insignia), franja de demo, "Aquí falla", etiqueta "Internet" y el texto
  "Mejorando… confirmando".
- La raíz ya quedó limpia: las copias de la v1 se enviaron a la Papelera (siguen en `legacy/`) y el
  .zip de la v1 está en `legacy/` (ignorado por git). `.github/workflows/build.yml` ya existe; el
  release de una etiqueta `v*` se crea como borrador.
- Por revisar: al recuperarse de un Wi-Fi débil, la ventana de mediciones aún guarda las pérdidas
  viejas y durante ~30 s se culpa al router o al proveedor ("cambio de causa" con la misma
  gravedad). Se vio en la demo; falta confirmar si pasa con redes reales.
- Decisiones abiertas de Juan:
  - Dónde alojar el feed de actualizaciones (Azure Blob o un servidor propio; GitHub Releases
    tiene límite de consultas por IP).
  - Certificado de firma de código (OV de una CA, o Azure Trusted Signing si Colombia es elegible).
  - `supportName`, `supportUrl` y `supportEmail` reales en `defaults.json` (usan Zoho Desk como
    mesa de ayuda).
- Ideas a futuro: botones de acción en los avisos (requiere el SDK de Windows / AppNotification),
  y un tablero de Power BI plantilla sobre los CSV compartidos.
- Primer instalador generado (24 sep.): `artifacts\releases\InternetHealthMonitor-win-Setup.exe`
  (77 MB, sin firma y sin `updateFeedUrl`: sirve para pruebas y un piloto, no para repartir a todos).
- Nada está commiteado todavía: los cambios de la v2 están como pendientes en la rama `dev`.
  Commitea solo cuando Juan lo pida.
