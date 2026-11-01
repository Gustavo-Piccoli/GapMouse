<#
.SYNOPSIS
  Generates the GapMouse .ico files (app icon and tray state icons).
.DESCRIPTION
  Works in Windows PowerShell 5.1 and pwsh on Windows (System.Drawing only).
  Each icon is a filled circle in the state color with a white stylized mouse,
  stored as PNG entries at 16, 20, 24, 32, 40, 48, 64 and 256 px.
#>
[CmdletBinding()]
param(
    [string]$OutputDirectory
)

Add-Type -AssemblyName System.Drawing

if (-not $OutputDirectory) {
    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
    $OutputDirectory = Join-Path $scriptDir '..\..\src\GapMouse.App\Assets\Icons'
}

$sizes = 16, 20, 24, 32, 40, 48, 64, 256
$icons = [ordered]@{
    'gapmouse.ico'     = '#3B82F6'
    'tray-waiting.ico' = '#3B82F6'
    'tray-moving.ico'  = '#22C55E'
    'tray-paused.ico'  = '#F59E0B'
}

function New-RoundedRectPath([single]$x, [single]$y, [single]$w, [single]$h, [single]$r) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $path.AddArc($x, $y, $d, $d, 180, 90)
    $path.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $path.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $path.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-IconPng([int]$size, [System.Drawing.Color]$color) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    $fill = New-Object System.Drawing.SolidBrush $color
    $g.FillEllipse($fill, [single]0, [single]0, [single]($size - 1), [single]($size - 1))

    # Mouse body: rounded rectangle centered in the circle, about 36% x 62% of the size.
    $bw = $size * 0.36
    $bh = $size * 0.62
    $bx = ($size - $bw) / 2
    $by = ($size - $bh) / 2
    $white = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
    $body = New-RoundedRectPath $bx $by $bw $bh ($bw / 2)
    $g.FillPath($white, $body)

    # Wheel: short vertical line in the state color, upper half of the body.
    $penWidth = [Math]::Max(1.0, $size * 0.06)
    $pen = New-Object System.Drawing.Pen $color, ([single]$penWidth)
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $cx = $size / 2
    $g.DrawLine($pen, [single]$cx, [single]($by + $bh * 0.14), [single]$cx, [single]($by + $bh * 0.38))

    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bytes = $ms.ToArray()

    $pen.Dispose(); $body.Dispose(); $white.Dispose(); $fill.Dispose(); $ms.Dispose(); $g.Dispose(); $bmp.Dispose()
    # Unary comma keeps the byte[] from being unrolled by the pipeline.
    return , $bytes
}

function Write-Ico([string]$path, [string]$hex) {
    $color = [System.Drawing.ColorTranslator]::FromHtml($hex)
    $images = foreach ($s in $sizes) { , (New-IconPng $s $color) }

    $stream = [System.IO.File]::Create($path)
    $w = New-Object System.IO.BinaryWriter $stream
    $w.Write([uint16]0)               # reserved
    $w.Write([uint16]1)               # type: icon
    $w.Write([uint16]$sizes.Count)    # image count

    $offset = 6 + 16 * $sizes.Count
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $s = $sizes[$i]
        $dim = if ($s -ge 256) { 0 } else { $s }   # 0 means 256 in the ICO directory
        $w.Write([byte]$dim)
        $w.Write([byte]$dim)
        $w.Write([byte]0)             # palette
        $w.Write([byte]0)             # reserved
        $w.Write([uint16]1)           # planes
        $w.Write([uint16]32)          # bits per pixel
        $w.Write([uint32]$images[$i].Length)
        $w.Write([uint32]$offset)
        $offset += $images[$i].Length
    }
    foreach ($img in $images) { $w.Write([byte[]]$img) }
    $w.Dispose()
    $stream.Dispose()
}

$null = New-Item -ItemType Directory -Force -Path $OutputDirectory
foreach ($name in $icons.Keys) {
    $target = Join-Path (Resolve-Path $OutputDirectory) $name
    Write-Ico $target $icons[$name]
    Write-Host "Wrote $target"
}
