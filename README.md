<!--
  SPDX-License-Identifier: GPL-3.0-or-later
  Copyright (C) 2026 Shinra CC
-->

# ShinraShot

A fast, cross-platform screenshot tool in the spirit of **Greenshot** and **Flameshot** —
one codebase for **Windows**, **Debian/Ubuntu**, and **RHEL/Fedora**. Built to match or beat
Greenshot on the capture → action-menu → editor workflow, with a live zoom magnifier during
region selection.

Dark-first UI. No telemetry, no accounts, no store.

## Top features (the bar: as good as or better than Greenshot)

- **Capture Region** — drag-select any rectangle, with a **live zoom magnifier** under the
  cursor and pixel-precise crosshair + live dimensions.
- **Capture Full Screen** — all monitors or the active one.
- **PNG output** at full quality (lossless), plus configurable scaling.
- **Open in Editor** — a built-in annotation editor with the Greenshot tool set
  (rectangle, ellipse, line, arrow, freehand, text, highlight, obfuscate/blur/pixelate,
  step counter, crop, resize, effects), and the same destination options.
- **Save to folder**, **Save directly** (auto-name to a configured folder, no dialog),
  **Save as…**, and **Copy to clipboard**.
- **Destination picker** after each capture (Greenshot-style action menu) — or a configured
  default for one-key capture.

See **[ROADMAP.md](ROADMAP.md)** for the full feature-parity matrix and build order.

## Stack

- **.NET 10** + **Avalonia UI 12** (cross-platform, single codebase).
- Platform-specific screen capture / clipboard / global-hotkey code is isolated behind
  interfaces in `ShinraShot.Core`; Windows uses Win32 (GDI/DXGI), Linux uses X11 and the
  `xdg-desktop-portal` screenshot portal (with `grim` fallback on wlroots/Wayland).

## Layout

```
shinraShot/
├── src/
│   ├── ShinraShot.Core/     platform-agnostic model + capture/clipboard/hotkey abstractions
│   └── ShinraShot.App/      Avalonia UI: tray, region overlay, editor, settings
├── tests/                   unit tests (ShinraShot.Core.Tests)
├── installer/               Windows NSIS installer + build-installer.ps1 (publish→sign→compile→sign)
├── linux/                   .desktop file, .deb/.rpm packaging, install.sh
└── assets/                  brand art: source PNG, generated ICO, Linux PNG icon set
```

## Build & run (Windows, from the repo root)

```powershell
dotnet build ShinraShot.slnx -c Debug
dotnet run --project src/ShinraShot.App     # launches to the system tray
```

Requires the **.NET 10 SDK** (pinned in `global.json`).

## Updates

ShinraShot checks for new releases over the network. On launch it quietly checks a published
`update.json` manifest and, when a newer build exists for the running platform, shows a toast;
**Settings → Updates** (and the tray's **Check for Updates…**) has a download button that fetches
the matching installer (Windows exe, `.deb`, or `.rpm`) with progress and a checksum check, then
lets you open it. Nothing installs on its own. Point the check at a different manifest by setting
the `SHINRASHOT_UPDATE_FEED` environment variable.

## License

**GPL-3.0-or-later** (see [LICENSE](LICENSE)) — matching Greenshot's and Flameshot's license,
so their source may be studied while ShinraShot is reimplemented clean. Sharing a build means
sharing the source.
