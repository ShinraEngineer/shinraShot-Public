#Requires -Version 5.1
<#
  SPDX-License-Identifier: GPL-3.0-or-later
  Copyright (C) 2026 Shinra CC

  build-installer.ps1 -- build the ShinraShot Windows installer.

  Pipeline: publish (self-contained win-x64) -> generate dark branding bitmaps
  -> compile with makensis -> optionally sign -> report.

  Examples:
    .\build-installer.ps1
    .\build-installer.ps1 -Configuration Release -Sign -CertSubject "Shinra CC"
    .\build-installer.ps1 -NsisKitInclude "D:\kit\include" -Makensis "D:\NSIS\makensis.exe"
#>
[CmdletBinding()]
param(
  [string]$Configuration = 'Release',
  [string]$Runtime       = 'win-x64',

  # Path to the house NSIS kit 'include' directory (holds ShinraCommon.nsh).
  [string]$NsisKitInclude = 'C:\Users\scook\Projects\workProjects\scriptsToolsServices\NSIS\NSIS Starter (Modern Shinra Version)\include',

  [string]$Makensis = 'C:\Program Files (x86)\NSIS\makensis.exe',

  # Code signing (opt in). Either a cert subject in the user store, or a PFX.
  [switch]$Sign,
  [string]$CertSubject,
  [string]$PfxPath,
  [string]$PfxPassword,
  [string]$TimestampUrl = 'http://timestamp.digicert.com',
  [string]$Signtool
)

$ErrorActionPreference = 'Stop'

function Write-Step([string]$msg) { Write-Host "==> $msg" -ForegroundColor Cyan }
function Write-Info([string]$msg) { Write-Host "    $msg" -ForegroundColor DarkGray }

# ---- paths ------------------------------------------------------------------
$installerDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot     = Split-Path -Parent $installerDir
$appProject   = Join-Path $repoRoot 'src\ShinraShot.App\ShinraShot.App.csproj'
$iconSource   = Join-Path $repoRoot 'assets\icons\shinrashot-256.png'
$nsiScript    = Join-Path $installerDir 'shinrashot.nsi'

$buildDir     = Join-Path $installerDir 'build'
$publishDir   = Join-Path $buildDir 'publish'
$brandingDir  = Join-Path $buildDir 'branding'

# ---- version (single source of truth: Directory.Build.props VersionPrefix) --
$propsPath = Join-Path $repoRoot 'Directory.Build.props'
$propsXml  = [xml](Get-Content -Raw -LiteralPath $propsPath)
$display   = ([string]$propsXml.Project.PropertyGroup.VersionPrefix).Trim()
if ([string]::IsNullOrWhiteSpace($display)) { throw "VersionPrefix not found in $propsPath" }
$fourPart  = ($display -split '\.')
while ($fourPart.Count -lt 4) { $fourPart += '0' }
$appVersion = ($fourPart[0..3] -join '.')              # e.g. 0.1.0.0
$outFile    = Join-Path $buildDir ("ShinraShot-{0}-Setup.exe" -f $display)

Write-Step "ShinraShot installer build"
Write-Info  "version     : $display  ($appVersion)"
Write-Info  "configuration: $Configuration / $Runtime"
Write-Info  "repo        : $repoRoot"

# ---- preflight --------------------------------------------------------------
if (-not (Test-Path -LiteralPath $Makensis)) {
  throw "makensis not found at '$Makensis'. Install NSIS 3.x or pass -Makensis <path>."
}
$kitFile = Join-Path $NsisKitInclude 'ShinraCommon.nsh'
if (-not (Test-Path -LiteralPath $kitFile)) {
  throw "House NSIS kit include not found ('$kitFile'). Pass -NsisKitInclude <path to the kit 'include' dir>."
}
if (-not (Test-Path -LiteralPath $iconSource)) {
  throw "Icon source not found at '$iconSource'."
}

# ---- 1. publish -------------------------------------------------------------
Write-Step "Publishing $Runtime self-contained"
if (Test-Path -LiteralPath $publishDir) { Remove-Item -Recurse -Force -LiteralPath $publishDir }
New-Item -ItemType Directory -Force -Path $publishDir | Out-Null

& dotnet publish $appProject `
  -c $Configuration `
  -r $Runtime `
  --self-contained true `
  -p:PublishSingleFile=false `
  -p:DebugType=none `
  -p:DebugSymbols=false `
  -p:SatelliteResourceLanguages=en `
  -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed ($LASTEXITCODE)." }

$exePath = Join-Path $publishDir 'ShinraShot.exe'
if (-not (Test-Path -LiteralPath $exePath)) { throw "Publish did not produce ShinraShot.exe." }

# Drop debug symbols. DebugType=none only suppresses the managed PDBs; the native
# SkiaSharp / HarfBuzz packages ship their own large .pdb files (~100 MB) that end
# users never need. Remove every .pdb from the payload.
$pdbs = Get-ChildItem -Recurse -LiteralPath $publishDir -Filter *.pdb -ErrorAction SilentlyContinue
if ($pdbs) {
  $pdbMB = [math]::Round(($pdbs | Measure-Object Length -Sum).Sum / 1MB, 1)
  $pdbs | Remove-Item -Force
  Write-Info "removed $($pdbs.Count) debug symbol file(s) ($pdbMB MB)"
}

$publishSize = [math]::Round((Get-ChildItem -Recurse -LiteralPath $publishDir | Measure-Object Length -Sum).Sum / 1MB, 1)
Write-Info "published $publishSize MB to $publishDir"

# ---- 2. branding bitmaps ----------------------------------------------------
Write-Step "Generating dark branding bitmaps (Shinra Aero Glass)"
New-Item -ItemType Directory -Force -Path $brandingDir | Out-Null
Add-Type -AssemblyName System.Drawing

# Theme tokens (see THEME.md -> SHINRA_THEME.md).
$base      = [System.Drawing.Color]::FromArgb(0x22, 0x28, 0x34)   # #222834
$textCol   = [System.Drawing.Color]::FromArgb(0xF2, 0xF5, 0xFA)   # #F2F5FA
$subCol    = [System.Drawing.Color]::FromArgb(0xA7, 0xB0, 0xBF)   # #A7B0BF
$src       = [System.Drawing.Image]::FromFile($iconSource)

function New-Bmp([int]$w, [int]$h, [scriptblock]$draw) {
  $bmp = New-Object System.Drawing.Bitmap($w, $h, [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
  $g   = [System.Drawing.Graphics]::FromImage($bmp)
  $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $g.PixelOffsetMode   = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
  $g.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
  $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::ClearTypeGridFit
  $g.Clear($base)
  & $draw $g
  $g.Dispose()
  return $bmp
}

# header.bmp -- 150x57, icon right-aligned with a small margin.
$header = New-Bmp 150 57 {
  param($g)
  $m = 4
  $scale = [Math]::Min((57 - 2*$m) / $src.Height, (57 - 2*$m) / $src.Width)
  if ($scale -gt 1.0) { $scale = 1.0 }
  $dw = [int]($src.Width * $scale); $dh = [int]($src.Height * $scale)
  $g.DrawImage($src, (150 - $dw - $m), [int]((57 - $dh) / 2), $dw, $dh)
}
$header.Save((Join-Path $brandingDir 'header.bmp'), [System.Drawing.Imaging.ImageFormat]::Bmp)
$header.Dispose()

# welcome.bmp -- 164x314, icon centered in the upper area, wordmark below.
$welcome = New-Bmp 164 314 {
  param($g)
  $icon = 96
  $g.DrawImage($src, [int]((164 - $icon) / 2), 56, $icon, $icon)

  $title = New-Object System.Drawing.Font('Segoe UI', 15, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
  $tag   = New-Object System.Drawing.Font('Segoe UI', 10, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
  $fmt   = New-Object System.Drawing.StringFormat
  $fmt.Alignment = [System.Drawing.StringAlignment]::Center
  $tb = New-Object System.Drawing.SolidBrush($textCol)
  $sb = New-Object System.Drawing.SolidBrush($subCol)
  $g.DrawString('ShinraShot', $title, $tb, (New-Object System.Drawing.RectangleF(0, 172, 164, 24)), $fmt)
  $g.DrawString('Capture. Annotate. Share.', $tag, $sb, (New-Object System.Drawing.RectangleF(0, 198, 164, 20)), $fmt)
  $title.Dispose(); $tag.Dispose(); $fmt.Dispose(); $tb.Dispose(); $sb.Dispose()
}
$welcome.Save((Join-Path $brandingDir 'welcome.bmp'), [System.Drawing.Imaging.ImageFormat]::Bmp)
$welcome.Dispose()
$src.Dispose()
Write-Info "wrote header.bmp (150x57) and welcome.bmp (164x314)"

# ---- 3. compile -------------------------------------------------------------
Write-Step "Compiling installer with makensis"
if (Test-Path -LiteralPath $outFile) { Remove-Item -Force -LiteralPath $outFile }

$nsisArgs = @(
  '/V3',
  '/WX',
  ("/DSHINRA_KIT_INCLUDE=$NsisKitInclude"),
  ("/DPUBLISH_DIR=$publishDir"),
  ("/DBRANDING_DIR=$brandingDir"),
  ("/DAPP_VERSION=$appVersion"),
  ("/DAPP_DISPLAY_VERSION=$display"),
  ("/DOUT_FILE=$outFile"),
  $nsiScript
)
& $Makensis @nsisArgs
if ($LASTEXITCODE -ne 0) { throw "makensis failed ($LASTEXITCODE)." }
if (-not (Test-Path -LiteralPath $outFile)) { throw "makensis reported success but '$outFile' is missing." }

# ---- 4. sign (opt in) -------------------------------------------------------
if ($Sign) {
  Write-Step "Signing"
  if (-not $Signtool) {
    $cand = Get-ChildItem 'C:\Program Files (x86)\Windows Kits\10\bin' -Recurse -Filter 'signtool.exe' -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match 'x64' } | Sort-Object FullName -Descending | Select-Object -First 1
    if ($cand) { $Signtool = $cand.FullName }
  }
  if (-not $Signtool -or -not (Test-Path -LiteralPath $Signtool)) {
    throw "signtool.exe not found. Pass -Signtool <path>, or install the Windows SDK."
  }
  $common = @('sign', '/fd', 'SHA256', '/tr', $TimestampUrl, '/td', 'SHA256')
  if ($PfxPath) {
    $args = $common + @('/f', $PfxPath)
    if ($PfxPassword) { $args += @('/p', $PfxPassword) }
    $args += $outFile
  } elseif ($CertSubject) {
    $args = $common + @('/n', $CertSubject, $outFile)
  } else {
    throw "Pass -CertSubject <name> or -PfxPath <file> with -Sign."
  }
  & $Signtool @args
  if ($LASTEXITCODE -ne 0) { throw "signtool failed ($LASTEXITCODE)." }
  Write-Info "signed"
} else {
  Write-Info "not signed (pass -Sign to sign). Unsigned installers trip SmartScreen."
}

# ---- 5. report --------------------------------------------------------------
$size = [math]::Round((Get-Item -LiteralPath $outFile).Length / 1MB, 1)
Write-Step "Done"
Write-Host ("    {0}  ({1} MB)" -f $outFile, $size) -ForegroundColor Green
