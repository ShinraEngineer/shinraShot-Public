<!--
  SPDX-License-Identifier: GPL-3.0-or-later
  Copyright (C) 2026 Shinra CC
-->

# ShinraShot Windows installer

An NSIS (Modern UI 2) installer, built the Shinra way. The dark wizard theme
follows the house color guide; see `THEME.md` at the repo root and the master
`SHINRA_THEME.md` it points to.

## Files

- `shinrashot.nsi` — the installer script (MUI2, dark theme, components,
  Add/Remove Programs footprint, matching uninstaller).
- `build-installer.ps1` — the build driver: publish, branding, compile, sign.
- `build/` — generated output (git-ignored): published app, branding bitmaps,
  and the finished `ShinraShot-<version>-Setup.exe`.

## Prerequisites

- .NET 10 SDK (pinned in `global.json`).
- NSIS 3.x (`makensis.exe`), by default at
  `C:\Program Files (x86)\NSIS\makensis.exe`.
- The house NSIS kit, for `include\ShinraCommon.nsh`. The default location is
  `scriptsToolsServices\NSIS\NSIS Starter (Modern Shinra Version)\include`.

## Build

```powershell
cd installer
.\build-installer.ps1
```

The version comes from `Directory.Build.props` (`VersionPrefix`), so the output
is named `build\ShinraShot-<version>-Setup.exe`.

Override the tool locations when they differ from the defaults:

```powershell
.\build-installer.ps1 -Makensis 'D:\NSIS\makensis.exe' `
                      -NsisKitInclude 'D:\kit\include'
```

## Signing

Signing is opt in. Unsigned installers trip SmartScreen.

```powershell
# from a certificate in the user's store, by subject name
.\build-installer.ps1 -Sign -CertSubject "Shinra CC"

# or from a PFX file
.\build-installer.ps1 -Sign -PfxPath C:\certs\shinra.pfx -PfxPassword '***'
```

## What the installer does

- Installs to `C:\Program Files\ShinraShot` (64-bit), elevation required.
- Components: the application (required) and an optional desktop shortcut. A
  Start-menu shortcut is always created.
- Registers an Add/Remove Programs entry with a quiet-uninstall string, so the
  app removes cleanly and supports `Uninstall.exe /S`.
- Offers to launch ShinraShot on the finish page.

## Silent install

```powershell
ShinraShot-<version>-Setup.exe /S            # silent, default components
ShinraShot-<version>-Setup.exe /S /D=C:\Apps\ShinraShot   # /D last, unquoted
```
