# Despliegue sin Intune

Como las licencias de Microsoft 365 no incluyen Intune, la app se distribuye con **Velopack**:

- Instalación **por usuario**, sin permisos de administrador, en `%LOCALAPPDATA%\InternetHealthMonitor`.
- Crea accesos directos, se registra para iniciar con Windows y se desinstala desde
  *Configuración → Aplicaciones* como cualquier programa.
- **Actualizaciones automáticas y silenciosas**: la app revisa cada 4 horas, descarga en segundo
  plano solo la diferencia (paquetes delta) y se reinicia en ~2 s cuando no hay una llamada en curso.

## 1. Generar una versión

```powershell
.\build\build.ps1 -Version 2.0.0 -Pack
```

En `artifacts\releases` quedan:

| Archivo | Para qué |
|---|---|
| `InternetHealthMonitor-win-Setup.exe` | El instalador que reciben los usuarios. |
| `releases.win.json`, `*-full.nupkg`, `*-delta.nupkg` | El "feed" de actualizaciones. |

También se puede hacer desde GitHub: al crear una etiqueta `v2.0.1`, el workflow compila, prueba y
publica el release.

## 2. Publicar el feed de actualizaciones

Sirve cualquier carpeta accesible por HTTPS. Luego se pone esa URL en `updateFeedUrl` de
`defaults.json` **antes** de compilar la primera versión que se distribuya. Opciones:

1. **Azure Blob Storage (sitio estático)**: cuesta centavos al mes. Se suben los archivos con
   `vpk upload az` o con Azure Storage Explorer.
2. **Un servidor web de la empresa** (IIS, el mismo de Magento, etc.): se copian los archivos a una
   carpeta pública.
3. **GitHub Releases**: `updateFeedUrl: "https://github.com/jcardila/internet-health-monitor"`. Solo
   sirve si el repositorio es público. Tiene límite de consultas por IP, que se nota en oficinas con
   muchos equipos detrás de la misma IP.

Para 400 personas se recomienda la opción 1 o 2.

## 3. Primera instalación en los equipos

Sin Intune, hay tres caminos, de menor a mayor automatización:

- **Enlace**: enviar el `Setup.exe` (o un enlace de SharePoint/OneDrive) por Teams con una
  instrucción de una línea. La instalación no pide administrador ni hace preguntas.
- **Script de inicio de sesión** (si los equipos están en un dominio de Active Directory): una GPO
  que ejecute `\\servidor\apps\InternetHealthMonitor-win-Setup.exe --silent` una sola vez por usuario.
- **Herramienta de soporte remoto** que ya usen (Zoho Assist, etc.): lanzar el mismo comando.

Después de la primera instalación, las actualizaciones llegan solas.

## 4. Firma de código (recomendado)

Sin firma, Windows SmartScreen muestra *"Windows protegió su PC"* al abrir el instalador descargado
y algunos antivirus desconfían del ejecutable. Opciones:

- **Certificado OV de una CA** (Sectigo, SSL.com, DigiCert…), hoy normalmente en token o HSM en la
  nube: `.\build\build.ps1 -Pack -SignParams "/a /fd sha256 /tr http://timestamp.digicert.com /td sha256"`.
- **Azure Trusted Signing**: más económico. Verifica primero que la empresa cumpla los requisitos de
  elegibilidad para Colombia; vpk lo soporta con `--azureTrustedSignFile`.

## 5. Configurar por sede (opcional)

Para que una sede pruebe sus propios servidores o muestre otro contacto de soporte, deja un
`C:\ProgramData\InternetHealthMonitor\defaults.json` en esos equipos. Tiene prioridad sobre el
archivo incluido en el instalador.
