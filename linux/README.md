<!--
  SPDX-License-Identifier: GPL-3.0-or-later
  Copyright (C) 2026 Shinra CC
-->

# ShinraShot Linux packaging

Builds a `.deb` (Debian/Ubuntu) and an `.rpm` (RHEL/Fedora) from a
self-contained `linux-x64` publish. This covers X11 and XWayland sessions;
the native Wayland portal capture path is a later slice.

## Files

- `build-packages.sh` — the build driver: publish → stage an FHS tree →
  `.deb` + `.rpm` → report.
- `shinrashot.desktop` — the application menu entry (installed as
  `cc.shinra.shinrashot.desktop`).
- `shinrashot.spec` — the RPM spec (driven by `build-packages.sh`).
- `build/`, `dist/` — generated output (git-ignored): staging tree and the
  finished packages.

## Build

```bash
cd linux
./build-packages.sh
```

Outputs land in `dist/`:

- `shinrashot_<version>_amd64.deb`
- `shinrashot-<version>-1.<dist>.x86_64.rpm`

The version comes from `Directory.Build.props` (`VersionPrefix`).

### Packaging without the .NET SDK

The publish step needs the SDK. To package a bundle built elsewhere (for
example, cross-published on another host), point `PUBLISH_DIR` at a ready-made
`linux-x64` publish and the publish step is skipped:

```bash
PUBLISH_DIR=/path/to/publish ./build-packages.sh
```

`dpkg-deb` and `rpmbuild` are each optional: without `dpkg-deb` the `.deb` is
assembled with `ar`/`tar`; without `rpmbuild` the `.rpm` is skipped.

## What the packages install

- The app under `/opt/shinrashot/`, launched via `/usr/bin/shinrashot`.
- A desktop entry at `/usr/share/applications/cc.shinra.shinrashot.desktop`.
- Icons under `/usr/share/icons/hicolor/<size>/apps/cc.shinra.shinrashot.png`
  (16–256 px); post-install refreshes the desktop and icon caches.
- The GPL-3.0 license text (`/usr/share/licenses/shinrashot/` on RPM,
  `/usr/share/doc/shinrashot/copyright` on Debian).

Runtime library dependencies (X11, Xrandr, fontconfig, Mesa GL, ICE/SM, …) are
declared in each package; the rest of the runtime ships inside the bundle.

## Install

```bash
sudo apt install ./shinrashot_<version>_amd64.deb      # Debian/Ubuntu
sudo dnf install ./shinrashot-<version>-1.*.x86_64.rpm  # RHEL/Fedora
```
