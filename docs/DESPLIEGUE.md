# Despliegue sin Intune

Como las licencias de Microsoft 365 no incluyen Intune, la app se distribuye con **Velopack**:

- Instalación **por usuario**, sin permisos de administrador, en `%LOCALAPPDATA%\InternetHealthMonitor`.
- Crea accesos directos, se registra para iniciar con Windows y se desinstala desde
  *Configuración → Aplicaciones* como cualquier programa.
- **Actualizaciones automáticas y silenciosas** desde GitHub Releases: descarga en segundo plano solo
  la diferencia (paquete delta) y se reinicia en ~2 s cuando no hay una llamada ni ventanas abiertas.

## 1. Publicar una versión (el camino normal)

1. Sube el número de versión en el CHANGELOG y haz commit en `main`.
2. Crea y sube la etiqueta: `git tag v2.0.1` y `git push origin v2.0.1`.
3. GitHub Actions prueba, compila, arma el instalador y lo deja como **borrador** en
   *Releases* (con la actualización delta respecto a la versión anterior).
4. Revisa el borrador (puedes descargar el `Setup.exe` y probarlo) y pulsa **Publish release**.
   Desde ese momento es el "latest" y los equipos se actualizan solos en las siguientes 24 h.

Mientras no lo publiques, nadie recibe la versión: el borrador es la compuerta de calidad.

Para compilar a mano en el PC: `.\build\build.ps1 -Version 2.0.1 -Pack` deja todo en
`artifacts\releases` (el instalador es `InternetHealthMonitor-win-Setup.exe`).

## 2. Cómo buscan actualizaciones los equipos

`updateFeedUrl` en `defaults.json` apunta a
`https://github.com/jcardila/internet-health-monitor/releases/latest/download`.

- Son **descargas directas** de archivos, no la API de GitHub: no aplica el límite de 60 consultas
  por hora por IP de la API (verificado: la API devuelve `X-RateLimit-Limit: 60`, la descarga no).
- Aun así se reparte la carga (`UpdateSchedule`): al iniciar se espera un tiempo al azar de 2 a
  20 min; si la consulta responde, la siguiente es en ~24 h (la fecha queda guardada y no se repite
  al reiniciar); si falla, reintenta en 1 h, 2 h, 4 h… hasta 8 h.
- Solo cuenta el release **publicado** más reciente: los borradores y los *pre-release* no llegan a
  los equipos.
- Si alguien está varias versiones atrás, Velopack descarga el paquete completo (~70 MB) en vez del
  delta. Es normal.

Si algún día el repositorio pasa a privado, hay que mover el feed a Azure Blob Storage u otro
servidor de archivos estáticos (`vpk upload az`) y cambiar `updateFeedUrl`: no se debe meter un
token de GitHub dentro de la app.

## 3. Primera instalación en los equipos

Sin Intune, hay tres caminos, de menor a mayor automatización:

- **Enlace**: enviar el enlace del release (o el `Setup.exe`) por Teams con una instrucción de una
  línea. La instalación no pide administrador ni hace preguntas.
- **Script de inicio de sesión** (si los equipos están en un dominio de Active Directory): una GPO
  que ejecute `\\servidor\apps\InternetHealthMonitor-win-Setup.exe --silent` una sola vez por usuario.
- **Herramienta de soporte remoto** que ya usen (Zoho Assist, etc.): lanzar el mismo comando.

Después de la primera instalación, las actualizaciones llegan solas.

## 4. Firma de código (pendiente)

Por ahora el instalador **no está firmado**: al abrirlo descargado, Windows muestra
*"Windows protegió su PC"* y hay que pulsar *Más información → Ejecutar de todas formas*.

- Azure Artifact Signing (antes Trusted Signing) con certificado público **no está disponible para
  empresas de Colombia** (sep. 2026).
- La opción recomendada para cuando se reparta a todos: **certificado OV con firma en la nube**
  (SSL.com eSigner, DigiCert KeyLocker…), para que GitHub Actions firme cada versión:
  `.\build\build.ps1 -Pack -SignParams "..."`.
- Ningún certificado (ni el EV) elimina el aviso desde el primer día: muestra "Grupo Ardisa" como
  editor y la reputación se construye con las descargas.

## 5. Configurar por sede (opcional)

Para que una sede pruebe sus propios servidores o muestre otro contacto de soporte, deja un
`C:\ProgramData\InternetHealthMonitor\defaults.json` en esos equipos. Tiene prioridad sobre el
archivo incluido en el instalador.
