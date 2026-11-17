#!/usr/bin/env bash
set -euo pipefail

VERSION="${1:?Usage: build-dmg.sh <version> <app_dir> <cli_dir> <out_dir>}"
APP_DIR="${2:?}"
CLI_DIR="${3:?}"
OUT_DIR="${4:?}"

ROOT_DIR="$(cd "$(dirname "$0")/../.." && pwd)"
WORK_DIR="$(mktemp -d)"
trap 'rm -rf "$WORK_DIR"' EXIT

APP_BUNDLE="$WORK_DIR/OpenSintegra Contabil.app"
mkdir -p "$APP_BUNDLE/Contents/MacOS" "$APP_BUNDLE/Contents/Resources"

cp -a "$APP_DIR"/. "$APP_BUNDLE/Contents/MacOS/"
cp -a "$CLI_DIR"/opensintegra-cli "$APP_BUNDLE/Contents/MacOS/"
chmod +x "$APP_BUNDLE/Contents/MacOS/OpenSintegra" "$APP_BUNDLE/Contents/MacOS/opensintegra-cli"

# Build macOS .icns from extracted 256x256 PNG
ICONSET="$WORK_DIR/OpenSintegra.iconset"
mkdir -p "$ICONSET"
SRC_PNG="$ROOT_DIR/assets/brand/app-256.png"
sips -z 16 16     "$SRC_PNG" --out "$ICONSET/icon_16x16.png"      >/dev/null
sips -z 32 32     "$SRC_PNG" --out "$ICONSET/icon_16x16@2x.png"   >/dev/null
sips -z 32 32     "$SRC_PNG" --out "$ICONSET/icon_32x32.png"      >/dev/null
sips -z 64 64     "$SRC_PNG" --out "$ICONSET/icon_32x32@2x.png"   >/dev/null
sips -z 128 128   "$SRC_PNG" --out "$ICONSET/icon_128x128.png"    >/dev/null
sips -z 256 256   "$SRC_PNG" --out "$ICONSET/icon_128x128@2x.png" >/dev/null
sips -z 256 256   "$SRC_PNG" --out "$ICONSET/icon_256x256.png"    >/dev/null
cp "$SRC_PNG" "$ICONSET/icon_256x256@2x.png"
iconutil -c icns "$ICONSET" -o "$APP_BUNDLE/Contents/Resources/OpenSintegra.icns"

cat > "$APP_BUNDLE/Contents/Info.plist" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key>
  <string>OpenSintegra Contabil</string>
  <key>CFBundleDisplayName</key>
  <string>OpenSintegra Contabil</string>
  <key>CFBundleIdentifier</key>
  <string>io.opensintegra.contabil</string>
  <key>CFBundleVersion</key>
  <string>${VERSION}</string>
  <key>CFBundleShortVersionString</key>
  <string>${VERSION}</string>
  <key>CFBundleExecutable</key>
  <string>OpenSintegra</string>
  <key>CFBundleIconFile</key>
  <string>OpenSintegra</string>
  <key>CFBundlePackageType</key>
  <string>APPL</string>
  <key>NSHighResolutionCapable</key>
  <true/>
</dict>
</plist>
EOF

mkdir -p "$OUT_DIR"
STAGE="$WORK_DIR/dmg-stage"
mkdir -p "$STAGE"
cp -R "$APP_BUNDLE" "$STAGE/"
ln -s /Applications "$STAGE/Applications"

hdiutil create -volname "OpenSintegra Contabil ${VERSION}" -srcfolder "$STAGE" -ov -format UDZO \
  "$OUT_DIR/OpenSintegra-${VERSION}-osx-arm64.dmg"
