#!/usr/bin/env bash
# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 Shinra CC
#
# build-packages.sh -- build the ShinraShot Linux packages (.deb + .rpm).
#
# Pipeline: publish (self-contained linux-x64) -> stage an FHS tree
# (/opt/shinrashot + /usr/bin launcher + .desktop + hicolor icons + license)
# -> assemble a .deb (dpkg-deb, or a portable ar/tar fallback) -> build an .rpm
# (rpmbuild) -> report. Outputs land in linux/dist/.
#
# The build host needs the .NET SDK to publish. To package on a host without the
# SDK (e.g. an RPM box packaging a bundle built elsewhere), point PUBLISH_DIR at a
# ready-made linux-x64 publish and the publish step is skipped:
#
#   PUBLISH_DIR=/path/to/publish ./build-packages.sh
#
# Either tool may be missing: a .deb is still produced without dpkg-deb, and the
# .rpm step is skipped (with a note) when rpmbuild is absent.

set -euo pipefail

SCRIPT_DIR=$(cd "$(dirname "$0")" && pwd)
REPO=$(cd "$SCRIPT_DIR/.." && pwd)
RID=linux-x64
CONFIG=Release
DIST="$SCRIPT_DIR/dist"
STAGE="$SCRIPT_DIR/build/stage"
PUBLISH_DIR="${PUBLISH_DIR:-}"

MAINTAINER="Shinra CC <6942488+ShinraEngineer@users.noreply.github.com>"
APPROOT_ABS="/opt/shinrashot"           # where the bundle installs on the target
ICON_SIZES="16 24 32 48 64 128 256"

step() { printf '\033[36m==> %s\033[0m\n' "$*"; }
info() { printf '    %s\n' "$*"; }

# ---- version (single source of truth: Directory.Build.props) ----------------
VERSION=$(sed -n 's:.*<VersionPrefix>\(.*\)</VersionPrefix>.*:\1:p' "$REPO/Directory.Build.props" | head -1)
[ -n "$VERSION" ] || { echo "VersionPrefix not found in Directory.Build.props" >&2; exit 1; }
step "ShinraShot Linux packages  version=$VERSION  rid=$RID"

# ---- 1. publish into the staged /opt tree -----------------------------------
rm -rf "$STAGE" "$DIST" "$SCRIPT_DIR/build/debroot" "$SCRIPT_DIR/build/rpm"
mkdir -p "$STAGE$APPROOT_ABS" "$DIST"
if [ -n "$PUBLISH_DIR" ]; then
  step "Reusing publish at $PUBLISH_DIR"
  cp -a "$PUBLISH_DIR/." "$STAGE$APPROOT_ABS/"
else
  command -v dotnet >/dev/null 2>&1 || { echo "dotnet not found; set PUBLISH_DIR to a prebuilt $RID publish." >&2; exit 1; }
  step "Publishing $RID self-contained"
  dotnet publish "$REPO/src/ShinraShot.App/ShinraShot.App.csproj" \
    -c "$CONFIG" -r "$RID" --self-contained true \
    -p:PublishSingleFile=false -p:DebugType=none -p:DebugSymbols=false \
    -o "$STAGE$APPROOT_ABS"
fi
[ -f "$STAGE$APPROOT_ABS/ShinraShot" ] || { echo "publish did not produce ShinraShot" >&2; exit 1; }
chmod 0755 "$STAGE$APPROOT_ABS/ShinraShot"

# ---- 2. FHS tree: launcher, desktop entry, icons, license -------------------
step "Staging FHS tree"
mkdir -p "$STAGE/usr/bin"
ln -sf "$APPROOT_ABS/ShinraShot" "$STAGE/usr/bin/shinrashot"
install -Dm644 "$SCRIPT_DIR/shinrashot.desktop" "$STAGE/usr/share/applications/cc.shinra.shinrashot.desktop"
for n in $ICON_SIZES; do
  install -Dm644 "$REPO/assets/icons/shinrashot-$n.png" \
    "$STAGE/usr/share/icons/hicolor/${n}x${n}/apps/cc.shinra.shinrashot.png"
done
install -Dm644 "$REPO/LICENSE" "$STAGE/usr/share/licenses/shinrashot/LICENSE"
INSTALLED_KB=$(du -sk "$STAGE" | cut -f1)
info "staged $INSTALLED_KB KiB"

# ---- 3. .deb ----------------------------------------------------------------
build_deb() {
  step "Building .deb"
  local debroot="$SCRIPT_DIR/build/debroot"
  rm -rf "$debroot"; mkdir -p "$debroot"
  cp -a "$STAGE/." "$debroot/"
  rm -rf "$debroot/usr/share/licenses"                        # rpm-ism; Debian uses doc/copyright
  install -Dm644 "$REPO/LICENSE" "$debroot/usr/share/doc/shinrashot/copyright"

  mkdir -p "$debroot/DEBIAN"
  cat > "$debroot/DEBIAN/control" <<EOF
Package: shinrashot
Version: $VERSION
Architecture: amd64
Maintainer: $MAINTAINER
Installed-Size: $INSTALLED_KB
Section: graphics
Priority: optional
Depends: libc6, libstdc++6, zlib1g, libfontconfig1, libgl1, libx11-6, libxext6, libxrandr2, libxi6, libxcursor1, libxrender1, libxinerama1, libxcb1, libice6, libsm6
Recommends: xclip, wl-clipboard
Description: Capture, annotate and share screenshots
 ShinraShot is a cross-platform screenshot tool in the Greenshot/Flameshot vein:
 region and full-screen capture with a live magnifier, and an annotation editor
 (arrows, text, highlight, blur/pixelate, step counters). This build targets X11
 and XWayland sessions.
EOF

  local maint="$debroot/DEBIAN/postinst"
  cat > "$maint" <<'EOF'
#!/bin/sh
set -e
command -v update-desktop-database >/dev/null 2>&1 && update-desktop-database -q /usr/share/applications || true
command -v gtk-update-icon-cache  >/dev/null 2>&1 && gtk-update-icon-cache -q -t /usr/share/icons/hicolor || true
EOF
  cp "$maint" "$debroot/DEBIAN/postrm"
  chmod 0755 "$debroot/DEBIAN/postinst" "$debroot/DEBIAN/postrm"

  local out="$DIST/shinrashot_${VERSION}_amd64.deb"
  if command -v dpkg-deb >/dev/null 2>&1; then
    dpkg-deb --root-owner-group --build "$debroot" "$out" >/dev/null
  else
    info "dpkg-deb absent; assembling with ar/tar"
    local tmp; tmp=$(mktemp -d)
    echo "2.0" > "$tmp/debian-binary"
    ( cd "$debroot/DEBIAN" && tar --numeric-owner --owner=0 --group=0 -czf "$tmp/control.tar.gz" ./* )
    ( cd "$debroot" && tar --numeric-owner --owner=0 --group=0 --exclude=./DEBIAN -czf "$tmp/data.tar.gz" . )
    # dpkg requires this member order: debian-binary, control, data.
    ( cd "$tmp" && ar rcD "$out" debian-binary control.tar.gz data.tar.gz )
    rm -rf "$tmp"
  fi
  info "wrote $out"
}

# ---- 4. .rpm ----------------------------------------------------------------
build_rpm() {
  if ! command -v rpmbuild >/dev/null 2>&1; then
    info ".rpm skipped (rpmbuild not found; install the rpm-build package)"
    return 0
  fi
  step "Building .rpm"
  local top="$SCRIPT_DIR/build/rpm"
  mkdir -p "$top"/{BUILD,RPMS,SOURCES,SPECS,SRPMS,BUILDROOT}
  rpmbuild \
    --define "_topdir $top" \
    --define "appversion $VERSION" \
    --define "appstage $STAGE" \
    -bb "$SCRIPT_DIR/shinrashot.spec" >/dev/null
  local rpm; rpm=$(find "$top/RPMS" -name '*.rpm' | head -1)
  [ -n "$rpm" ] || { echo "rpmbuild produced no .rpm" >&2; return 1; }
  cp "$rpm" "$DIST/"
  info "wrote $DIST/$(basename "$rpm")"
}

build_deb
build_rpm

# ---- 5. report --------------------------------------------------------------
step "Done"
ls -la "$DIST"
