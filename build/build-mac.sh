#!/usr/bin/env bash
# Compila la versión para Mac: pruebas + publicación autocontenida + paquete .app
# y, con --pack, el instalador y el paquete de actualización de Velopack.
#
#   ./build/build-mac.sh                          # .app local en artifacts/mac/<arch>/
#   ./build/build-mac.sh --version 2.1.0 --pack   # + instalador (.pkg) en artifacts/releases-mac/<arch>
#   ./build/build-mac.sh --arch x64 ...           # Mac con procesador Intel (por defecto arm64 = Apple M1 o superior)
#   --github-repo https://github.com/dueño/repo   # baja el release anterior para crear el delta (CI)
#   --github-token TOKEN                          # opcional: evita el límite de consultas de la API de GitHub
#   --skip-tests                                  # no correr las pruebas (el CI ya las corrió)
set -euo pipefail

VERSION="2.0.0"
ARCH="arm64"
PACK=0
GITHUB_REPO=""
GITHUB_TOKEN=""
SKIP_TESTS=0
VPK_VERSION="1.2.158" # igual que el paquete Velopack del proyecto

while [[ $# -gt 0 ]]; do
  case "$1" in
    --version) VERSION="$2"; shift 2 ;;
    --arch) ARCH="$2"; shift 2 ;;
    --pack) PACK=1; shift ;;
    --github-repo) GITHUB_REPO="$2"; shift 2 ;;
    --github-token) GITHUB_TOKEN="$2"; shift 2 ;;
    --skip-tests) SKIP_TESTS=1; shift ;;
    *) echo "Opción desconocida: $1" >&2; exit 2 ;;
  esac
done
[[ "$ARCH" == "arm64" || "$ARCH" == "x64" ]] || { echo "--arch debe ser arm64 o x64" >&2; exit 2; }

REPO="$(cd "$(dirname "$0")/.." && pwd)"
# Si el repositorio está en OneDrive (u otra carpeta de ~/Library/CloudStorage), su File Provider
# bloquea a ratos la escritura de .dll ("Access denied") y agrega atributos que rompen la firma.
# En ese caso se compila y firma en una copia fuera de OneDrive y solo se traen los resultados.
if [[ "$REPO" == *"/Library/CloudStorage/"* ]]; then
  ROOT="${TMPDIR:-/tmp}/internethealth-build"
  echo "==> Copiando el código a $ROOT (fuera de OneDrive)"
  mkdir -p "$ROOT"
  rsync -a --delete --exclude bin/ --exclude obj/ --exclude artifacts/ --exclude .git/ --exclude .DS_Store "$REPO/" "$ROOT/"
else
  ROOT="$REPO"
fi
OUT="$ROOT/artifacts/mac/$ARCH"
PUBLISH="$OUT/publish"
# Nombre de carpeta sin tildes (pkgbuild falla con "ó"); macOS muestra "Monitor de Conexión"
# gracias al nombre localizado (build/mac/es.lproj/InfoPlist.strings).
APP="$OUT/Monitor de Conexion.app"
RID="osx-$ARCH"
# Canal de actualizaciones: Apple Silicon usa el canal por defecto de Velopack en Mac ("osx");
# Intel tiene el suyo para no recibir el paquete de la otra arquitectura (ver UpdateService.cs).
CHANNEL=$([[ "$ARCH" == "arm64" ]] && echo "osx" || echo "osx-x64")

if [[ $SKIP_TESTS -eq 0 ]]; then
  echo "==> Pruebas"
  dotnet run --project "$ROOT/tests/InternetHealth.Core.Tests" -c Release
fi

echo "==> Publicando $RID (versión $VERSION)"
pkill -x InternetHealthMonitor 2>/dev/null || true # la instancia abierta bloquea la instancia única
rm -rf "$OUT"
dotnet publish "$ROOT/src/InternetHealth.Mac/InternetHealth.Mac.csproj" -c Release -r "$RID" --self-contained \
  -p:Version="$VERSION" -o "$PUBLISH"

echo "==> Armando $(basename "$APP")"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp -R "$PUBLISH/." "$APP/Contents/MacOS/"
rm -f "$APP/Contents/MacOS/"*.pdb
cp "$ROOT/build/mac/AppIcon.icns" "$APP/Contents/Resources/AppIcon.icns"
cp -R "$ROOT/build/mac/es.lproj" "$APP/Contents/Resources/"
sed "s/__VERSION__/$VERSION/g" "$ROOT/build/mac/Info.plist" > "$APP/Contents/Info.plist"
# Firma "ad hoc" (sin certificado): obligatoria en Apple Silicon. Sin firma de Apple, la primera
# vez hay que abrirla desde Ajustes del Sistema → Privacidad y seguridad → "Abrir de todas formas".
# OneDrive y el Finder agregan atributos extendidos que codesign rechaza.
xattr -cr "$APP"
codesign --force --deep --sign - "$APP"
echo "    $APP"

if [[ $PACK -eq 1 ]]; then
  export PATH="$PATH:$HOME/.dotnet/tools"
  # vpk es una herramienta de .NET: necesita saber dónde está .NET (Homebrew lo instala en otra ruta).
  if [[ -z "${DOTNET_ROOT:-}" ]]; then
    sdk_dir="$(dotnet --list-sdks | tail -1 | sed -E 's/.*\[(.*)\]/\1/')"
    export DOTNET_ROOT="$(dirname "$sdk_dir")"
  fi
  if ! vpk --help >/dev/null 2>&1; then dotnet tool update -g vpk --version "$VPK_VERSION"; fi
  RELEASES="$ROOT/artifacts/releases-mac/$ARCH"
  mkdir -p "$RELEASES"
  if [[ -n "$GITHUB_REPO" ]]; then
    echo "==> Descargando el release anterior ($CHANNEL) para crear el delta"
    TOKEN_ARGS=(); [[ -n "$GITHUB_TOKEN" ]] && TOKEN_ARGS=(--token "$GITHUB_TOKEN")
    vpk download github --repoUrl "$GITHUB_REPO" --channel "$CHANNEL" ${TOKEN_ARGS[@]+"${TOKEN_ARGS[@]}"} -o "$RELEASES" || echo "    (no hay release anterior para Mac: se publica sin delta)"
  fi
  echo "==> Empaquetando con Velopack (canal $CHANNEL)"
  vpk pack --packId InternetHealthMonitor --packVersion "$VERSION" --packDir "$APP" \
    --mainExe InternetHealthMonitor --packTitle "Monitor de Conexion" --packAuthors "jcardila" \
    --icon "$ROOT/build/mac/AppIcon.icns" \
    --plist "$APP/Contents/Info.plist" --runtime "$RID" --channel "$CHANNEL" -o "$RELEASES"
  echo "    $RELEASES"
  ls -la "$RELEASES"
fi

if [[ "$ROOT" != "$REPO" ]]; then
  echo "==> Copiando resultados a $REPO/artifacts"
  mkdir -p "$REPO/artifacts/mac/$ARCH"
  rm -rf "$REPO/artifacts/mac/$ARCH/$(basename "$APP")"
  ditto "$APP" "$REPO/artifacts/mac/$ARCH/$(basename "$APP")" # ditto conserva la firma de las .dll
  if [[ $PACK -eq 1 ]]; then
    mkdir -p "$REPO/artifacts/releases-mac"
    rm -rf "$REPO/artifacts/releases-mac/$ARCH"
    ditto "$ROOT/artifacts/releases-mac/$ARCH" "$REPO/artifacts/releases-mac/$ARCH"
  fi
  echo "    $REPO/artifacts/mac/$ARCH/$(basename "$APP")"
fi
