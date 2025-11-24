# Guía de Versionamiento

Este documento explica cómo versionar y liberar nuevas versiones de Internet Health Monitor.

---

## 📌 Sistema de Versiones

Este proyecto usa **[Semantic Versioning 2.0.0](https://semver.org/)**:

```
MAJOR.MINOR.PATCH
```

- **MAJOR**: Cambios incompatibles (breaking changes)
- **MINOR**: Nueva funcionalidad compatible hacia atrás
- **PATCH**: Correcciones de bugs compatibles hacia atrás

### Ejemplos:

- `1.0.0` → `1.0.1`: Fix de bug
- `1.0.1` → `1.1.0`: Nueva funcionalidad
- `1.1.0` → `2.0.0`: Cambio que rompe compatibilidad

---

## 🔄 Proceso para Crear una Nueva Versión

### Paso 1: Actualizar el Número de Versión

Edita **solo este archivo**: `InternetHealth.ps1`

Busca la línea (aprox. línea 4):

```powershell
# Version: 1.0.0
```

Cambia a la nueva versión:

```powershell
# Version: 1.0.1
```

**¡Eso es todo!** El script `create-release-package.ps1` lee automáticamente la versión de aquí.

### Paso 2: Actualizar CHANGELOG.md

Agrega una nueva sección al inicio de `CHANGELOG.md`:

```markdown
## [1.0.1] - 2025-11-25

### Fixed

- Corregido problema con detección de gateway en redes complejas
- Mejorado manejo de errores en config.json

### Changed

- Actualizado texto de diagnóstico para mayor claridad

[1.0.1]: https://github.com/jcardila/internet-health-monitor/releases/tag/v1.0.1
```

### Paso 3: Commit y Push

```bash
git add .
git commit -m "Release v1.0.1"
git push origin main
```

### Paso 4: Crear el Paquete ZIP

```powershell
.\create-release-package.ps1
```

Esto creará automáticamente: `InternetHealthMonitor-v1.0.1.zip`

### Paso 5: Crear el Release en GitHub

1. Ve a: https://github.com/jcardila/internet-health-monitor/releases/new
2. **Tag**: `v1.0.1`
3. **Title**: `Internet Health Monitor v1.0.1`
4. **Description**: Copia la sección relevante del CHANGELOG.md
5. **Adjunta**: El ZIP generado en el Paso 4
6. **Publish release**

### Paso 6: Crear y Push el Tag (Opcional si no lo hiciste desde GitHub)

```bash
git tag -a v1.0.1 -m "Version 1.0.1"
git push origin v1.0.1
```

---

## 📝 Plantilla de Descripción de Release

Para releases MINOR o PATCH:

```markdown
# Internet Health Monitor v1.0.1

## 🐛 Correcciones / 🎉 Novedades

- Descripción breve de los cambios
- Otro cambio importante
- Fix de bug crítico

## 📥 Instalación

Descarga `InternetHealthMonitor-v1.0.1.zip` y ejecuta `RUN_ME.bat`

## 📋 Changelog Completo

Ver [CHANGELOG.md](https://github.com/jcardila/internet-health-monitor/blob/main/CHANGELOG.md)
```

Para releases MAJOR:

```markdown
# Internet Health Monitor v2.0.0 🎉

## ⚠️ Breaking Changes

- Descripción de cambios incompatibles
- Cómo migrar de v1.x a v2.0

## ✨ Nuevas Funcionalidades

- Lista de nuevas características

## 📥 Instalación

Descarga `InternetHealthMonitor-v2.0.0.zip` y ejecuta `RUN_ME.bat`
```

---

## 🎯 Checklist Rápido

Antes de publicar un release:

- [ ] Versión actualizada en `InternetHealth.ps1` (línea 4)
- [ ] Versión coincide entre script y tag (ambos `v1.0.1`)
- [ ] `CHANGELOG.md` actualizado con los cambios
- [ ] Link del release agregado al final de `CHANGELOG.md`
- [ ] Commit y push realizados
- [ ] ZIP generado con `create-release-package.ps1`
- [ ] Release publicado en GitHub con el ZIP adjunto
- [ ] Tag creado y pusheado (si no se hizo desde GitHub)

---

## 🔍 Verificar la Versión Actual

### Desde la Aplicación

La versión aparece en el título de la ventana: `Internet Health Monitor v1.0.0`

### Desde el Script

```powershell
Get-Content InternetHealth.ps1 | Select-String "# Version:"
```

### Desde Git (Source of Truth)

```bash
git describe --tags --abbrev=0
```

### Desde GitHub

https://github.com/jcardila/internet-health-monitor/releases/latest

---

## 🎯 Cómo Funciona el Versionamiento y Auto-Actualización

### **Sistema Híbrido de Versiones**

El script usa un **enfoque híbrido inteligente**:

1. **Intenta leer el tag de Git** (si está disponible):
   ```powershell
   git describe --tags --abbrev=0
   ```
2. **Si no hay Git**, lee del header del script:

   ```powershell
   # Version: 1.0.0
   ```

3. **Ventajas**:
   - ✅ Si trabajas desde el repo con Git → usa el tag (source of truth)
   - ✅ Si distribuyes el `.ps1` solo → usa la versión hardcoded
   - ✅ Siempre funciona, incluso sin Git instalado
   - ✅ La versión aparece automáticamente en:
     - Título de la ventana
     - Reportes exportados
     - Logs del sistema

### Flujo de Versión y Auto-Actualización

```
┌─────────────────────────────┐
│  Desarrollador crea release │
└──────────┬──────────────────┘
           │
           ▼
┌─────────────────────────────┐
│ 1. Actualiza # Version: X.Y │
└──────────┬──────────────────┘
           │
           ▼
┌─────────────────────────────┐
│ 2. Commit & Push            │
└──────────┬──────────────────┘
           │
           ▼
┌─────────────────────────────┐
│ 3. Crea Git tag vX.Y        │
│ 4. Crea GitHub Release      │
└──────────┬──────────────────┘
           │
           ▼
┌─────────────────────────────┐
│  GitHub tiene nueva versión │
└──────────┬──────────────────┘
           │
           ▼
┌─────────────────────────────┐
│  Usuario inicia la app      │
└──────────┬──────────────────┘
           │
           ▼
┌─────────────────────────────┐
│  App verifica GitHub API    │
│  (después de 3 segundos)    │
└──────────┬──────────────────┘
           │
        ┌──┴──┐
        │     │
        ▼     ▼
   [Nuevo] [Actual]
      │       │
      ▼       ▼
   [Popup]  [Log]
"Actualizar" "OK"
```

Ver [UPDATE_SYSTEM.md](UPDATE_SYSTEM.md) para detalles del sistema de actualización.

---

## 📚 Lugares Donde Aparece la Versión

| Archivo              | Línea    | Formato                   | Debe Actualizarse                  |
| -------------------- | -------- | ------------------------- | ---------------------------------- |
| `InternetHealth.ps1` | ~4       | `# Version: X.Y.Z`        | ✅ SÍ (único lugar obligatorio)    |
| `CHANGELOG.md`       | Variable | `## [X.Y.Z] - YYYY-MM-DD` | ✅ SÍ                              |
| `config.json`        | ~2       | `"version": "X.Y.Z"`      | ⚠️ Opcional (referencial)          |
| GitHub Tag           | -        | `vX.Y.Z`                  | ✅ SÍ (al crear release)           |
| README badges        | ~5       | `version-X.Y.Z-blue`      | ⚠️ Opcional (se puede automatizar) |

**Nota**: El único lugar crítico es `InternetHealth.ps1`. Todo lo demás es documentación.

---

## 🤖 Automatización Futura

Posibles mejoras para automatizar:

1. **GitHub Actions**: Crear release automáticamente al push de un tag
2. **Pre-commit hook**: Validar que la versión en CHANGELOG coincida con el script
3. **Script de bump version**: Automatizar el incremento de versión

---

## 🆘 Solución de Problemas

### "El ZIP tiene la versión incorrecta"

→ Verifica que `InternetHealth.ps1` tenga la versión correcta en la línea 4

### "El tag ya existe"

→ Elimina el tag local y remoto:

```bash
git tag -d v1.0.0
git push origin :refs/tags/v1.0.0
```

### "No puedo subir el release"

→ Verifica permisos en GitHub Settings > Actions > General

---

## 📞 Contacto

Si tienes dudas sobre el proceso de versionamiento, abre un issue en el repositorio.
