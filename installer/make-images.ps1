<#
.SYNOPSIS
    Draws the setup wizard images: the small logo at the top right of each page and the tall picture on the
    finish page, each in the sizes Inno Setup picks from for 100 to 250 percent display scaling.
.NOTES
    Run once after the logo changes; the images are part of the repository.
#>
param(
    [string]$Logo = (Join-Path $PSScriptRoot '..\assets\Square150x150Logo.png'),
    [string]$Out = (Join-Path $PSScriptRoot 'images')
)
Add-Type -AssemblyName System.Drawing
New-Item -ItemType Directory -Force $Out | Out-Null
$logoImage = [Drawing.Image]::FromFile((Resolve-Path $Logo))

function C($hex, $a = 255) { $c = [Drawing.ColorTranslator]::FromHtml($hex); [Drawing.Color]::FromArgb($a, $c.R, $c.G, $c.B) }
function Canvas($w, $h) {
    $bmp = New-Object Drawing.Bitmap $w, $h, ([Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'; $g.InterpolationMode = 'HighQualityBicubic'; $g.PixelOffsetMode = 'HighQuality'; $g.TextRenderingHint = 'AntiAliasGridFit'
    return $bmp, $g
}
function Rounded([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object Drawing.Drawing2D.GraphicsPath
    $p.AddArc($x, $y, 2*$r, 2*$r, 180, 90); $p.AddArc($x+$w-2*$r, $y, 2*$r, 2*$r, 270, 90)
    $p.AddArc($x+$w-2*$r, $y+$h-2*$r, 2*$r, 2*$r, 0, 90); $p.AddArc($x, $y+$h-2*$r, 2*$r, 2*$r, 90, 90); $p.CloseFigure(); $p
}
function Gradient($rect, [string[]]$colors, [float]$angle = 0) {
    $b = New-Object Drawing.Drawing2D.LinearGradientBrush $rect, (C $colors[0]), (C $colors[-1]), $angle
    $blend = New-Object Drawing.Drawing2D.ColorBlend $colors.Count
    $blend.Colors = [Drawing.Color[]]($colors | ForEach-Object { C $_ })
    $blend.Positions = [float[]](0..($colors.Count - 1) | ForEach-Object { $_ / ($colors.Count - 1) })
    $b.InterpolationColors = $blend; $b
}

# Small image, top right of each page: the logo on a transparent background (the header is light or dark).
$small = @(@(55, 58), @(64, 68), @(83, 80), @(92, 97), @(110, 106), @(119, 123), @(138, 140))
foreach ($size in $small) {
    $w = $size[0]; $h = $size[1]
    $bmp, $g = Canvas $w $h
    $edge = [Math]::Min($w, $h) * 0.92
    $g.DrawImage($logoImage, [float](($w - $edge) / 2), [float](($h - $edge) / 2), [float]$edge, [float]$edge)
    $g.Dispose(); $bmp.Save((Join-Path $Out "small-$w.png"), [Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
}

# The tall picture of the finish page is drawn separately and kept in images\ with the others.
$logoImage.Dispose()
Get-ChildItem $Out | ForEach-Object { $_.Name }
