#!/usr/bin/env bash
set -euo pipefail

VERSION="${1:?Usage: build-deb.sh <version> <app_dir> <cli_dir> <out_dir>}"
APP_DIR="${2:?}"
CLI_DIR="${3:?}"
OUT_DIR="${4:?}"

ROOT_DIR="$(cd "$(dirname "$0")/../.." && pwd)"
PKG_ROOT="$(mktemp -d)"
trap 'rm -rf "$PKG_ROOT"' EXIT

install -d "$PKG_ROOT/DEBIAN"
install -d "$PKG_ROOT/opt/opensintegra"
install -d "$PKG_ROOT/usr/bin"
install -d "$PKG_ROOT/usr/share/applications"
install -d "$PKG_ROOT/usr/share/icons/hicolor/256x256/apps"

cp -a "$APP_DIR"/. "$PKG_ROOT/opt/opensintegra/"
cp -a "$CLI_DIR"/opensintegra-cli "$PKG_ROOT/opt/opensintegra/"
chmod +x "$PKG_ROOT/opt/opensintegra/OpenSintegra" "$PKG_ROOT/opt/opensintegra/opensintegra-cli"

ln -s /opt/opensintegra/OpenSintegra "$PKG_ROOT/usr/bin/opensintegra"
ln -s /opt/opensintegra/opensintegra-cli "$PKG_ROOT/usr/bin/opensintegra-cli"

install -m 644 "$ROOT_DIR/packaging/linux/opensintegra.desktop" "$PKG_ROOT/usr/share/applications/opensintegra.desktop"
install -m 644 "$ROOT_DIR/assets/brand/app-256.png" "$PKG_ROOT/usr/share/icons/hicolor/256x256/apps/opensintegra.png"

cat > "$PKG_ROOT/DEBIAN/control" <<EOF
Package: opensintegra
Version: ${VERSION}
Section: utils
Priority: optional
Architecture: amd64
Maintainer: OpenSintegra Contributors <https://github.com/opensintegra/open-sintegra>
Description: Suite contabil para validacao e auditoria Sintegra (Convenio ICMS 57/95)
 Suite desktop multiplataforma e CLI de codigo aberto (MIT) para validacao,
 correcao automatica, auditoria cruzada XML/SPED e geracao de relatorios PDF/XLSX.
EOF

mkdir -p "$OUT_DIR"
dpkg-deb --build "$PKG_ROOT" "$OUT_DIR/opensintegra_${VERSION}_amd64.deb"
