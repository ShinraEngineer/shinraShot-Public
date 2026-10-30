; ============================================================================
;  shinrashot.nsi  --  ShinraShot Windows installer (NSIS 3.x, MUI2)
;  SPDX-License-Identifier: GPL-3.0-or-later
;  Copyright (C) 2026 Shinra CC
;
;  Built the Shinra way (see the house NSIS kit reference,
;  scriptsToolsServices\NSIS\NSIS Starter (Modern Shinra Version)).
;  Dark theme values come from the house color guide; see THEME.md at the repo
;  root and the master SHINRA_THEME.md it points to.
;
;  This script is driven by build-installer.ps1, which publishes the app,
;  generates the branding bitmaps, and passes every path in as a /D define.
;  Compile it by hand only with all defines supplied, e.g.:
;
;    makensis /DSHINRA_KIT_INCLUDE="...\include" /DPUBLISH_DIR="...\publish" \
;             /DBRANDING_DIR="...\branding" /DAPP_VERSION=0.1.0.0 \
;             /DAPP_DISPLAY_VERSION=0.1.0 /DOUT_FILE="...\ShinraShot-0.1.0-Setup.exe" \
;             installer\shinrashot.nsi
; ============================================================================

Unicode true
SetCompressor /SOLID lzma
SetCompressorDictSize 64

; ---- required build-time defines (build-installer.ps1 supplies all of them) --
!ifndef SHINRA_KIT_INCLUDE
  !error "SHINRA_KIT_INCLUDE is not defined. Run build-installer.ps1, or pass \
/DSHINRA_KIT_INCLUDE=<path to the house NSIS kit 'include' directory>."
!endif
!ifndef PUBLISH_DIR
  !error "PUBLISH_DIR is not defined. Run build-installer.ps1 (it publishes the \
app and passes the folder in)."
!endif
!ifndef BRANDING_DIR
  !error "BRANDING_DIR is not defined. Run build-installer.ps1 (it generates the \
header/welcome bitmaps and passes the folder in)."
!endif
!ifndef APP_VERSION
  !define APP_VERSION "0.0.0.0"
!endif
!ifndef APP_DISPLAY_VERSION
  !define APP_DISPLAY_VERSION "0.0.0"
!endif
!ifndef OUT_FILE
  !define OUT_FILE "${__FILEDIR__}\build\ShinraShot-Setup.exe"
!endif

; ---- theme (Shinra Aero Glass, Dark; see THEME.md / SHINRA_THEME.md 4.4) -----
; Define the colors BEFORE including MUI2 so they win over MUI's FFFFFF/000000
; defaults (MUI applies those at MUI_LANGUAGE time via MUI_DEFAULT). MUI only
; honors these on the Welcome, Finish, and header strips; the License /
; Components / Directory / InstFiles page bodies use system colors and cannot be
; themed dark without owner-drawing (see the house reference 3.2). The dark
; banner bitmaps match the Welcome/Finish pages, where it shows most.
!define MUI_BGCOLOR   "222834"        ; base surface
!define MUI_TEXTCOLOR "F2F5FA"        ; primary text

!addincludedir "${SHINRA_KIT_INCLUDE}"
!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "FileFunc.nsh"
!include "ShinraCommon.nsh"

; This installer uses a subset of the house include's helpers, so NSIS warns
; about the unused ones: 6010 (StrContains dead code, zeroed out; harmless per
; REFERENCE 22.3) and 6001 (the ShinraLog variable, unset). Both are expected;
; silence just these two and keep /WX catching everything else.
!pragma warning disable 6010
!pragma warning disable 6001

; ---- product identity -------------------------------------------------------
!define APP_NAME      "ShinraShot"
!define APP_PUBLISHER "Shinra CC"
!define APP_EXE       "ShinraShot.exe"
!define APP_URL       "https://shinra.cc"
!define APP_REGKEY    "Software\Shinra CC\ShinraShot"
!define UNINST_KEY    "Software\Microsoft\Windows\CurrentVersion\Uninstall\ShinraShot"

Name "${APP_NAME}"
OutFile "${OUT_FILE}"
InstallDir "$PROGRAMFILES64\ShinraShot"
InstallDirRegKey HKLM "${APP_REGKEY}" "InstallDir"
RequestExecutionLevel admin           ; writes Program Files + HKLM ARP
ShowInstDetails show
ShowUninstDetails show
BrandingText "${APP_NAME} ${APP_DISPLAY_VERSION}  -  ${APP_PUBLISHER}"

; ---- version resource -------------------------------------------------------
VIProductVersion "${APP_VERSION}"
VIFileVersion    "${APP_VERSION}"
VIAddVersionKey "ProductName"     "${APP_NAME}"
VIAddVersionKey "ProductVersion"  "${APP_DISPLAY_VERSION}"
VIAddVersionKey "FileVersion"     "${APP_VERSION}"
VIAddVersionKey "CompanyName"     "${APP_PUBLISHER}"
VIAddVersionKey "FileDescription" "${APP_NAME} Setup"
VIAddVersionKey "LegalCopyright"  "Copyright (C) 2026 ${APP_PUBLISHER}"

; ---- icons (must be defined before the page macros) -------------------------
!define MUI_ICON   "${__FILEDIR__}\..\assets\ShinraShot.ico"
!define MUI_UNICON "${__FILEDIR__}\..\assets\ShinraShot.ico"

; ---- header bitmap (150 x 57, 24-bpp), welcome/finish banner (164 x 314) -----
!define MUI_HEADERIMAGE
!define MUI_HEADERIMAGE_BITMAP          "${BRANDING_DIR}\header.bmp"
!define MUI_HEADERIMAGE_UNBITMAP        "${BRANDING_DIR}\header.bmp"
!define MUI_WELCOMEFINISHPAGE_BITMAP    "${BRANDING_DIR}\welcome.bmp"
!define MUI_UNWELCOMEFINISHPAGE_BITMAP  "${BRANDING_DIR}\welcome.bmp"

!define MUI_ABORTWARNING

; ---- welcome text (budgets: title <=2 lines, body ~28 caps/line; REFERENCE 3.1.1)
!define MUI_WELCOMEPAGE_TITLE "Install ShinraShot"
!define MUI_WELCOMEPAGE_TEXT  "This wizard installs ShinraShot, a fast screenshot and annotation tool.$\r$\n$\r$\nClick Next to continue."

; ---- finish page: offer to launch ------------------------------------------
!define MUI_FINISHPAGE_RUN            "$INSTDIR\${APP_EXE}"
!define MUI_FINISHPAGE_RUN_TEXT       "Launch ShinraShot"
!define MUI_FINISHPAGE_RUN_NOTCHECKED
!define MUI_FINISHPAGE_LINK           "Visit shinra.cc"
!define MUI_FINISHPAGE_LINK_LOCATION  "${APP_URL}"

; ---- installer pages --------------------------------------------------------
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_LICENSE "${__FILEDIR__}\..\LICENSE"
!define MUI_COMPONENTSPAGE_SMALLDESC
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

; ---- uninstaller pages ------------------------------------------------------
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

; ---- language MUST come after every page macro ------------------------------
!insertmacro MUI_LANGUAGE "English"

; ============================================================================
Section "ShinraShot (required)" SEC_CORE
  SectionIn RO
  SetOutPath "$INSTDIR"
  File /r "${PUBLISH_DIR}\*.*"

  ; Start-menu shortcut (machine-wide: SetShellVarContext all in .onInit)
  CreateDirectory "$SMPROGRAMS\${APP_NAME}"
  CreateShortcut  "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk" "$INSTDIR\${APP_EXE}" "" "$INSTDIR\${APP_EXE}" 0
  CreateShortcut  "$SMPROGRAMS\${APP_NAME}\Uninstall ${APP_NAME}.lnk" "$INSTDIR\Uninstall.exe"

  ; Our own config key
  WriteRegStr HKLM "${APP_REGKEY}" "InstallDir" "$INSTDIR"
  WriteRegStr HKLM "${APP_REGKEY}" "Version"    "${APP_DISPLAY_VERSION}"

  ; Add/Remove Programs footprint
  WriteRegStr   HKLM "${UNINST_KEY}" "DisplayName"          "${APP_NAME}"
  WriteRegStr   HKLM "${UNINST_KEY}" "DisplayVersion"       "${APP_DISPLAY_VERSION}"
  WriteRegStr   HKLM "${UNINST_KEY}" "Publisher"            "${APP_PUBLISHER}"
  WriteRegStr   HKLM "${UNINST_KEY}" "InstallLocation"      "$INSTDIR"
  WriteRegStr   HKLM "${UNINST_KEY}" "DisplayIcon"          '"$INSTDIR\${APP_EXE}",0'
  WriteRegStr   HKLM "${UNINST_KEY}" "URLInfoAbout"         "${APP_URL}"
  WriteRegStr   HKLM "${UNINST_KEY}" "UninstallString"      '"$INSTDIR\Uninstall.exe"'
  WriteRegStr   HKLM "${UNINST_KEY}" "QuietUninstallString" '"$INSTDIR\Uninstall.exe" /S'
  WriteRegDWORD HKLM "${UNINST_KEY}" "NoModify" 1
  WriteRegDWORD HKLM "${UNINST_KEY}" "NoRepair" 1

  ; Record the installed size for ARP (in KB).
  ${GetSize} "$INSTDIR" "/S=0K" $0 $1 $2
  IntFmt $0 "0x%08X" $0
  WriteRegDWORD HKLM "${UNINST_KEY}" "EstimatedSize" "$0"

  WriteUninstaller "$INSTDIR\Uninstall.exe"
SectionEnd

Section "Desktop shortcut" SEC_DESKTOP
  CreateShortcut "$DESKTOP\${APP_NAME}.lnk" "$INSTDIR\${APP_EXE}" "" "$INSTDIR\${APP_EXE}" 0
SectionEnd

; descriptions shown when a component is highlighted
!insertmacro MUI_FUNCTION_DESCRIPTION_BEGIN
  !insertmacro MUI_DESCRIPTION_TEXT ${SEC_CORE}    "The ShinraShot application and its runtime. Required."
  !insertmacro MUI_DESCRIPTION_TEXT ${SEC_DESKTOP} "Add a ShinraShot shortcut to the desktop."
!insertmacro MUI_FUNCTION_DESCRIPTION_END

; ============================================================================
Section "Uninstall"
  Delete "$DESKTOP\${APP_NAME}.lnk"
  Delete "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk"
  Delete "$SMPROGRAMS\${APP_NAME}\Uninstall ${APP_NAME}.lnk"
  RMDir  "$SMPROGRAMS\${APP_NAME}"

  ; Remove the install tree. Guard against a mis-set $INSTDIR before RMDir /r.
  ${If} $INSTDIR != ""
  ${AndIf} ${FileExists} "$INSTDIR\${APP_EXE}"
    Delete "$INSTDIR\Uninstall.exe"
    RMDir /r "$INSTDIR"
  ${EndIf}

  DeleteRegKey HKLM "${UNINST_KEY}"
  DeleteRegKey HKLM "${APP_REGKEY}"
  DeleteRegKey /ifempty HKLM "Software\Shinra CC"
SectionEnd

; ============================================================================
Function .onInit
  ${ShinraHelpHandler} "ShinraShot-Setup.exe" "Installs ShinraShot.$\r$\n  /S  Silent install.$\r$\n  /D=<dir>  Set the install directory (must be last, unquoted)."
  ${ShinraRequireX64}
  ${ShinraRequireAdmin}
  SetShellVarContext all      ; machine-wide shortcuts (admin install)
  SetRegView 64               ; real 64-bit registry, not the WOW mirror
FunctionEnd

Function un.onInit
  SetShellVarContext all
  SetRegView 64
  MessageBox MB_YESNO|MB_ICONQUESTION "Remove ${APP_NAME} and all of its files?" IDYES +2
    Abort
FunctionEnd
