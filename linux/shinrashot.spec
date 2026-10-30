# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 Shinra CC
#
# Packages a pre-staged, self-contained ShinraShot bundle into an .rpm.
# build-packages.sh drives this: it stages the FHS tree and passes the version
# and the stage directory as rpmbuild --define values; the install section just
# copies the staged tree into the buildroot. (No percent-macros in these
# comments: rpmbuild expands macros even inside hash comments.)
#
#   rpmbuild --define "_topdir <top>" --define "appversion X.Y.Z" \
#            --define "appstage <stage dir>" -bb shinrashot.spec

Name:           shinrashot
Version:        %{appversion}
Release:        1%{?dist}
Summary:        Capture, annotate and share screenshots
License:        GPL-3.0-or-later
URL:            https://github.com/ShinraEngineer/shinrashot
BuildArch:      x86_64

# The payload is a self-contained .NET bundle; its own libraries must not become
# auto-generated Provides/Requires. List the system libraries explicitly instead.
AutoReqProv:    no
Requires:       glibc, libstdc++, zlib, fontconfig, mesa-libGL
Requires:       libX11, libXext, libXrandr, libXi, libXcursor, libXrender, libXinerama, libxcb
Requires:       libICE, libSM
# Clipboard helpers: used at runtime to copy images (X11 / Wayland). Weak deps.
Recommends:     xclip
Recommends:     wl-clipboard

%description
ShinraShot is a cross-platform screenshot tool in the Greenshot/Flameshot vein:
region and full-screen capture with a live magnifier, and an annotation editor
(arrows, text, highlight, blur/pixelate, step counters). This build targets X11
and XWayland sessions.

%install
rm -rf %{buildroot}
mkdir -p %{buildroot}
cp -a %{appstage}/. %{buildroot}/

%files
/opt/shinrashot
/usr/bin/shinrashot
/usr/share/applications/cc.shinra.shinrashot.desktop
/usr/share/icons/hicolor/*/apps/cc.shinra.shinrashot.png
%license /usr/share/licenses/shinrashot/LICENSE

%post
command -v update-desktop-database >/dev/null 2>&1 && update-desktop-database -q /usr/share/applications || :
command -v gtk-update-icon-cache  >/dev/null 2>&1 && gtk-update-icon-cache -q -t /usr/share/icons/hicolor || :

%postun
command -v update-desktop-database >/dev/null 2>&1 && update-desktop-database -q /usr/share/applications || :
command -v gtk-update-icon-cache  >/dev/null 2>&1 && gtk-update-icon-cache -q -t /usr/share/icons/hicolor || :

%changelog
* Wed Oct 28 2026 Shinra CC <6942488+ShinraEngineer@users.noreply.github.com> - 0.1.0-1
- Initial Linux packaging (slice 7): X11/XWayland capture.
