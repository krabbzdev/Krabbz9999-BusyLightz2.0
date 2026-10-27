<#
.SYNOPSIS
  Builds the Blynclight tray app and its per-user MSI (no admin rights needed).
  Output: dist\Krabbz9999-Blynclight-App-Control-<version>.msi

.EXAMPLE
  .\build.ps1 -Version 2.0.3            # build only
  .\build.ps1 -Version 2.0.3 -Publish   # build, then publish as GitHub release v2.0.3 (installed apps auto-update to it)
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,
    # GitHub owner/repo the app checks for newer releases; empty disables auto-update.
    [string]$UpdateRepo = 'showtechclove1/Krabbz9999-Blynclight',
    # Publish the MSI as a GitHub release (needs GitHub CLI, signed in).
    [switch]$Publish
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$wix = Join-Path $root 'tools\wix'
$src = Join-Path $root 'src'
$assets = Join-Path $root 'assets'
$build = Join-Path $root 'build'
$dist = Join-Path $root 'dist'
New-Item -ItemType Directory -Force $assets, $build, $dist | Out-Null

if (-not (Test-Path "$wix\candle.exe")) {
    throw "WiX 3.14 not found in tools\wix. Download wix314-binaries.zip from https://github.com/wixtoolset/wix3/releases/tag/wix3141rtm and extract it there."
}

function Invoke-Tool([string]$exe, [string[]]$arguments) {
    & $exe @arguments
    if ($LASTEXITCODE -ne 0) { throw "$(Split-Path $exe -Leaf) failed with exit code $LASTEXITCODE" }
}

# Writes a multi-size .ico (PNG entries) of a light split red/blue.
function New-AppIcon([string]$path) {
    Add-Type -AssemblyName System.Drawing
    $sizes = 16, 32, 48, 256
    $pngs = foreach ($size in $sizes) {
        $bmp = New-Object System.Drawing.Bitmap $size, $size
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.SmoothingMode = 'AntiAlias'
        $pad = [Math]::Max(1, [int]($size / 16))
        $d = $size - 2 * $pad
        $g.FillPie([System.Drawing.Brushes]::Red, $pad, $pad, $d, $d, 135, 180)
        $g.FillPie([System.Drawing.Brushes]::RoyalBlue, $pad, $pad, $d, $d, 315, 180)
        $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(50, 50, 50)), ([Math]::Max(1, $size / 24))
        $g.DrawEllipse($pen, $pad, $pad, $d, $d)
        $g.Dispose()
        $ms = New-Object System.IO.MemoryStream
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $bmp.Dispose()
        , $ms.ToArray()
    }
    $out = New-Object System.IO.MemoryStream
    $w = New-Object System.IO.BinaryWriter $out
    $w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $dim = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
        $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
        $w.Write([uint16]1); $w.Write([uint16]32)
        $w.Write([uint32]$pngs[$i].Length); $w.Write([uint32]$offset)
        $offset += $pngs[$i].Length
    }
    foreach ($png in $pngs) { $w.Write($png) }
    [System.IO.File]::WriteAllBytes($path, $out.ToArray())
}

$icon = Join-Path $assets 'app.ico'
if (-not (Test-Path $icon)) { New-AppIcon $icon }

# Stamp the version and build date into the exe; the app shows them in its window and tray menu.
@"
using System.Reflection;
[assembly: AssemblyTitle("Krabbz9999 Blynclight App Control")]
[assembly: AssemblyProduct("Krabbz9999 Blynclight App Control")]
[assembly: AssemblyVersion("$Version.0")]
[assembly: AssemblyFileVersion("$Version.0")]
[assembly: AssemblyInformationalVersion("$Version")]
[assembly: AssemblyMetadata("BuildDate", "$(Get-Date -Format 'yyyy-MM-dd')")]
[assembly: AssemblyMetadata("UpdateRepo", "$UpdateRepo")]
"@ | Set-Content "$build\AssemblyInfo.cs" -Encoding utf8

Write-Host 'Compiling tray app...'
Invoke-Tool $csc @('/nologo', '/optimize+', '/target:winexe', "/win32icon:$icon",
    "/out:$build\Krabbz9999 Blynclight App Control.exe",
    '/reference:System.Core.dll', '/reference:System.Drawing.dll', '/reference:System.Windows.Forms.dll', '/reference:System.Web.Extensions.dll',
    "$src\BlyncCore.cs", "$src\LightController.cs", "$src\TrayApp.cs", "$src\Updater.cs", "$src\CallDetector.cs", "$build\AssemblyInfo.cs")

Write-Host 'Building MSI...'
Invoke-Tool "$wix\candle.exe" @('-nologo', '-arch', 'x64', '-ext', 'WixUtilExtension',
    "-dVersion=$Version", "-dBuildDir=$build", "-dAssetsDir=$assets",
    '-out', "$build\Product.wixobj", "$root\installer\Product.wxs")
$msi = Join-Path $dist "Krabbz9999-Blynclight-App-Control-$Version.msi"
Invoke-Tool "$wix\light.exe" @('-nologo', '-ext', 'WixUtilExtension', '-spdb', '-out', $msi, "$build\Product.wixobj")

Write-Host "Built $msi"

if ($Publish) {
    $gh = (Get-Command gh -ErrorAction SilentlyContinue).Source
    if (-not $gh) { $gh = Get-ChildItem "$env:LOCALAPPDATA\Microsoft\WinGet\Packages\GitHub.cli_*\bin\gh.exe" -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty FullName }
    if (-not $gh) { throw 'GitHub CLI (gh) not found. Install it with: winget install GitHub.cli' }
    # Upload under a fixed name so the README's "latest/download" link always gets the newest installer.
    $releaseDir = Join-Path $dist 'release'
    New-Item -ItemType Directory -Force $releaseDir | Out-Null
    $releaseMsi = Join-Path $releaseDir 'Krabbz9999-Blynclight-App-Control.msi'
    Copy-Item $msi $releaseMsi -Force
    Write-Host "Publishing release v$Version to $UpdateRepo..."
    Invoke-Tool $gh @('release', 'create', "v$Version", $releaseMsi, '--repo', $UpdateRepo,
        '--title', "Version $Version", '--notes', "Krabbz9999 Blynclight App Control $Version")
}
