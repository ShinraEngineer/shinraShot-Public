<!--
  SPDX-License-Identifier: GPL-3.0-or-later
  Copyright (C) 2026 Shinra CC
-->

# ShinraShot — Roadmap

Goal: a cross-platform (Windows, Debian, RHEL) screenshot tool that matches or beats
**Greenshot** on the capture → action-menu → editor workflow, and **Flameshot** on
fast in-overlay region capture with a live magnifier.

## Architecture

- **`ShinraShot.Core`** — no UI. Owns the domain model (capture requests/results, pixel
  buffers, PNG encode, settings schema) and the platform abstractions:
  - `ICaptureEngine` — grab full virtual desktop, a single screen, or a rectangle.
  - `IClipboardService` — put/get an image on the system clipboard.
  - `IGlobalHotkeys` — register OS-wide capture hotkeys.
  - `IScreenInfo` — monitor geometry + per-monitor DPI.
- **`ShinraShot.App`** — Avalonia. Tray host, the region-selection overlay (full-screen,
  topmost, per-monitor), the post-capture action menu, the editor, and settings.
- **Platform implementations** are selected at runtime by `OperatingSystem.Is*`:
  - Windows: Win32 `BitBlt`/DXGI capture, `OpenClipboard`/`CF_DIB`/`CF_DIBV5`, `RegisterHotKey`.
  - Linux X11: `XGetImage`/XShm, `xfixes` cursor, `_NET_*` geometry, X clipboard via Avalonia.
  - Linux Wayland: `org.freedesktop.portal.Screenshot` (and `grim` on wlroots) — no raw
    per-pixel grab, so the live magnifier falls back to a pre-grabbed frame.

## Build order (vertical slices; each slice is a runnable commit)

- [x] **Slice 0 — skeleton.** Solution, Core + App projects, GPL-3.0, generated icons,
      tray app that launches with a Capture/Settings/Quit menu. *(builds & runs)*
- [x] **Slice 1 — full-screen capture (Windows).** `ICaptureEngine` + Win32 backend,
      PNG encode, copy-to-clipboard, save-to-folder / save-directly. Prove end-to-end.
- [x] **Slice 2 — region overlay + magnifier.** Full-screen topmost overlay, dimmed
      backdrop, drag rectangle, crosshair, live **zoom magnifier** + dimensions + color
      readout, multi-monitor + DPI correct. Keyboard: Esc cancel, Enter confirm.
- [x] **Slice 3 — post-capture action menu.** Greenshot-style chooser: Copy, Save,
      Save as…, Open in editor, (later) Print/Upload. Plus a configurable default action.
- [x] **Slice 4 — editor (Greenshot parity).** See the tool matrix below.
- [x] **Slice 5 — settings + global hotkeys.** Settings window + persisted config
      (`%AppData%`/`$XDG_CONFIG_HOME`), OS-wide hotkeys (default `PrintScreen` / `Ctrl+PrintScreen`).
- [x] **Slice 6 — Windows installer.** NSIS (house `ShinraCommon.nsh`), `build-installer.ps1`
      (publish self-contained → sign → compile → sign), multi-res icon, ARP entry, autostart.
- [x] **Slice 7 — Linux.** X11 capture backend, Wayland portal path, `.desktop` + icons,
      `.deb` (Debian/Ubuntu) and `.rpm` (RHEL/Fedora) packaging; verify on WSL AlmaLinux-9.
- [ ] **Slice 8 — parity polish.** Window-capture (per-window), delayed capture, repeat-last,
      PNG scaling options, drop-shadow/torn-edge/border effects, counters.

## Greenshot editor parity matrix (Slice 4 target)

| Greenshot tool / option        | ShinraShot plan |
|--------------------------------|-----------------|
| Select / move / resize element | ✔ core editor interaction |
| Rectangle, Ellipse             | ✔ |
| Line, Arrow                    | ✔ (arrowheads both ends) |
| Freehand (pen)                 | ✔ |
| Text box                       | ✔ (font, size, color) |
| Speech bubble                  | ✔ |
| Step / counter (auto-number)   | ✔ |
| Highlight (area/text/grayscale)| ✔ highlight-area first |
| Obfuscate — blur / pixelate    | ✔ |
| Crop                           | ✔ |
| Rotate / flip                  | ✔ |
| Line thickness / color / fill  | ✔ shared property bar |
| Drop shadow / border / torn edge | Slice 8 |
| Undo / redo                    | ✔ full history stack |
| Destinations: clipboard, file, save-as, printer, imgur/email | clipboard/file/save-as in S4; print + uploaders later |
| Element z-order, duplicate     | ✔ |

## Non-goals (for now)

- Video/GIF capture, OCR, cloud accounts, auto-update server, telemetry.
- Light theme (dark-first, like ShinraSnap).
