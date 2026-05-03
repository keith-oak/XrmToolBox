#!/usr/bin/env bash
# Build XrmToolBox.app for macOS.
#
# Usage:
#   scripts/build-macos-app.sh                              # ad-hoc, ARM64, no zip
#   scripts/build-macos-app.sh --target osx-x64            # Intel
#   scripts/build-macos-app.sh --sign "Developer ID Application: Foo"
#   scripts/build-macos-app.sh --zip                        # produces dist/XrmToolBox-<version>-<rid>.zip

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SHELL_PROJ="${REPO_ROOT}/src/Shell"
ICON="${REPO_ROOT}/assets/icon/AppIcon.icns"
INFO_PLIST_TEMPLATE="${SHELL_PROJ}/Info.plist"
DIST_DIR="${REPO_ROOT}/dist"

RID="osx-arm64"
SIGN_IDENTITY=""
MAKE_ZIP=0

while (( $# > 0 )); do
  case "$1" in
    --target)
      RID="$2"; shift 2 ;;
    --sign)
      SIGN_IDENTITY="$2"; shift 2 ;;
    --zip)
      MAKE_ZIP=1; shift ;;
    --help|-h)
      grep -E '^# ' "$0" | head -8 | sed 's/^# //'; exit 0 ;;
    *)
      echo "unknown arg: $1" >&2; exit 2 ;;
  esac
done

VERSION=$(grep -oE '<Version>[^<]+</Version>' "${SHELL_PROJ}/XrmToolBox.MacOS.csproj" | head -1 | sed -E 's/<\/?Version>//g')
APP="${DIST_DIR}/PAC'd Toolbox.app"
EXE_NAME="PACdToolbox"

echo "→ build-macos-app: version=${VERSION} rid=${RID} sign=${SIGN_IDENTITY:-none} zip=${MAKE_ZIP}"

# 1. Clean dist
rm -rf "${APP}"
mkdir -p "${APP}/Contents/MacOS" "${APP}/Contents/Resources"

# 2. Publish self-contained
echo "→ dotnet publish (${RID})..."
dotnet publish "${SHELL_PROJ}" \
  -c Release \
  -r "${RID}" \
  --self-contained true \
  -p:PublishSingleFile=false \
  -p:UseAppHost=true \
  -nologo -v:q \
  -o "${DIST_DIR}/publish-${RID}" \
  > /dev/null

# 3. Assemble bundle
PUBLISH_DIR="${DIST_DIR}/publish-${RID}"
cp -R "${PUBLISH_DIR}/." "${APP}/Contents/MacOS/"

# Move runtime libraries into MacOS (Apple expects everything next to the executable)
# The executable itself is named XrmToolBox per <AssemblyName>.
chmod +x "${APP}/Contents/MacOS/${EXE_NAME}"

# 4. Icon
cp "${ICON}" "${APP}/Contents/Resources/AppIcon.icns"

# 5. Info.plist (substitute version)
sed "s/__VERSION__/${VERSION}/g" "${INFO_PLIST_TEMPLATE}" > "${APP}/Contents/Info.plist"

# 6. Plugins folder placement: keep alongside the executable so PluginManager finds them
#    (PluginManager uses AppContext.BaseDirectory which is Contents/MacOS for a .app)
if [ -d "${PUBLISH_DIR}/Plugins" ]; then
  echo "→ Plugins folder already inside published output"
else
  if [ -d "${REPO_ROOT}/src/Shell/bin/Release/net10.0/Plugins" ]; then
    cp -R "${REPO_ROOT}/src/Shell/bin/Release/net10.0/Plugins" "${APP}/Contents/MacOS/"
  fi
fi

# 7. Clear quarantine if present
xattr -dr com.apple.quarantine "${APP}" 2>/dev/null || true

# 8. Optional codesign
if [ -n "${SIGN_IDENTITY}" ]; then
  echo "→ codesign with ${SIGN_IDENTITY}..."
  codesign --force --deep --options runtime --timestamp \
    --sign "${SIGN_IDENTITY}" "${APP}"
  codesign --verify --deep --strict --verbose=2 "${APP}"
else
  # Ad-hoc sign so Gatekeeper at least lets the user open it after right-click
  codesign --force --deep --sign - "${APP}" 2>/dev/null || true
fi

# 9. Optional zip
if (( MAKE_ZIP == 1 )); then
  ZIP="${DIST_DIR}/PACdToolbox-${VERSION}-${RID}.zip"
  echo "→ zip → ${ZIP}"
  (cd "${DIST_DIR}" && rm -f "$(basename "${ZIP}")" && /usr/bin/ditto -c -k --keepParent "PAC'd Toolbox.app" "$(basename "${ZIP}")")
fi

# 10. Cleanup intermediate publish dir
rm -rf "${DIST_DIR}/publish-${RID}"

echo "✓ ${APP}"
ls -lah "${APP}/Contents/MacOS/${EXE_NAME}"
