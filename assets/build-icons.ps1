# Draws the SnipCanvas logo from vectors and writes:
#   snipcanvas.svg  the editable source
#   snipcanvas.png  1024 px, transparent
#   snipcanvas.ico  16, 20, 24, 32, 40, 48, 64, 128 and 256 px
# Each icon size is drawn from the vectors rather than scaled down, so small sizes stay crisp.
#   powershell -NoProfile -ExecutionPolicy Bypass -File assets\build-icons.ps1
#   powershell -NoProfile -ExecutionPolicy Bypass -File assets\build-icons.ps1 -Preview   # comparison sheet only
param([switch]$Preview, [string]$Out = $PSScriptRoot, [string]$Installer = (Join-Path (Split-Path -Parent $PSScriptRoot) 'installer'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore, WindowsBase

# ---- The design, on a 256 x 256 grid ---------------------------------------------------------------------------
# A white page (the canvas) tilted like a snipped photo, held inside four capture corners, marked up with a
# hand-drawn arrow in the same orange as the app's default annotation color.
$Design = @{
    Tile      = @(10, 10, 236, 236); TileRadius = 58
    TileStops = @(@('#8F7FFF', 0.0), @('#5F50F0', 0.5), @('#3A2FC2', 1.0))
    Page      = @(70, 70, 116, 116); PageRadius = 24; PageShadow = 8; PageTilt = -5.5; PagePivot = @(128, 128)
    Corners   = @('M48,92 L48,56 L84,56', 'M172,56 L208,56 L208,92', 'M48,164 L48,200 L84,200', 'M172,200 L208,200 L208,164')
    CornerInk = '#D9D4FF'; CornerWidth = 16
    Arrow     = 'M94,166 C102,128 128,108 162,104 M136,86 L162,104 L142,130'
    ArrowInk  = '#FF5B2E'; ArrowWidth = 21
}

function Col([string]$hex, [int]$alpha = 255) {
    $c = [System.Windows.Media.ColorConverter]::ConvertFromString($hex)
    [System.Windows.Media.Color]::FromArgb($alpha, $c.R, $c.G, $c.B)
}
function Fill([string]$hex, [int]$alpha = 255) { New-Object System.Windows.Media.SolidColorBrush (Col $hex $alpha) }
function Ink([string]$hex, [double]$width) {
    $pen = New-Object System.Windows.Media.Pen (Fill $hex), $width
    $pen.StartLineCap = 'Round'; $pen.EndLineCap = 'Round'; $pen.LineJoin = 'Round'
    $pen
}
function Shape([string]$data) { [System.Windows.Media.Geometry]::Parse($data) }
function Box($v) { New-Object System.Windows.Rect $v[0], $v[1], $v[2], $v[3] }
function Gradient([double]$x2, [double]$y2, $stops) {
    $g = New-Object System.Windows.Media.LinearGradientBrush
    $g.StartPoint = New-Object System.Windows.Point 0, 0
    $g.EndPoint = New-Object System.Windows.Point $x2, $y2
    foreach ($s in $stops) {
        $alpha = if ($s.Count -gt 2) { $s[2] } else { 255 }
        [void]$g.GradientStops.Add((New-Object System.Windows.Media.GradientStop (Col $s[0] $alpha), $s[1]))
    }
    $g
}

# k thickens the strokes a little at tiny sizes so they survive pixel snapping.
function Draw-Logo($dc, [double]$k) {
    $d = $Design
    $dc.DrawRoundedRectangle((Gradient 1 1 $d.TileStops), $null, (Box $d.Tile), $d.TileRadius, $d.TileRadius)
    $sheen = @(@('#FFFFFF', 0.0, 70), @('#FFFFFF', 0.5, 0))
    $dc.DrawRoundedRectangle((Gradient 0 1 $sheen), $null, (Box $d.Tile), $d.TileRadius, $d.TileRadius)
    $corners = Ink $d.CornerInk ($d.CornerWidth * $k)
    foreach ($path in $d.Corners) { $dc.DrawGeometry($null, $corners, (Shape $path)) }
    $dc.PushTransform((New-Object System.Windows.Media.RotateTransform $d.PageTilt, $d.PagePivot[0], $d.PagePivot[1]))
    $shadow = @($d.Page[0], ($d.Page[1] + $d.PageShadow), $d.Page[2], $d.Page[3])
    $dc.DrawRoundedRectangle((Fill '#1A1170' 70), $null, (Box $shadow), $d.PageRadius, $d.PageRadius)
    $dc.DrawRoundedRectangle((Fill '#FFFFFF'), $null, (Box $d.Page), $d.PageRadius, $d.PageRadius)
    $dc.DrawGeometry($null, (Ink $d.ArrowInk ($d.ArrowWidth * $k)), (Shape $d.Arrow))
    $dc.Pop()
}

function Render([int]$px) {
    $k = if ($px -le 24) { 1.3 } elseif ($px -le 40) { 1.12 } else { 1.0 }
    $visual = New-Object System.Windows.Media.DrawingVisual
    $dc = $visual.RenderOpen()
    $dc.PushTransform((New-Object System.Windows.Media.ScaleTransform ($px / 256.0), ($px / 256.0)))
    Draw-Logo $dc $k
    $dc.Pop(); $dc.Close()
    $bitmap = New-Object System.Windows.Media.Imaging.RenderTargetBitmap $px, $px, 96, 96, ([System.Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual); $bitmap.Freeze(); $bitmap
}

function Png-Bytes($bitmap) {
    $encoder = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
    $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = New-Object System.IO.MemoryStream; $encoder.Save($stream); ,$stream.ToArray()
}

# Small icon sizes are stored as classic 32-bit DIBs (best compatibility); 256 px is stored as PNG.
function Dib-Bytes($bitmap, [int]$px) {
    $converted = New-Object System.Windows.Media.Imaging.FormatConvertedBitmap $bitmap, ([System.Windows.Media.PixelFormats]::Bgra32), $null, 0
    $stride = $px * 4
    $pixels = New-Object byte[] ($stride * $px)
    $converted.CopyPixels($pixels, $stride, 0)
    $maskStride = [int]([Math]::Ceiling($px / 32.0) * 4)
    $stream = New-Object System.IO.MemoryStream
    $w = New-Object System.IO.BinaryWriter $stream
    $w.Write([int]40); $w.Write([int]$px); $w.Write([int]($px * 2)); $w.Write([int16]1); $w.Write([int16]32)
    $w.Write([int]0); $w.Write([int]($stride * $px + $maskStride * $px)); $w.Write([int]0); $w.Write([int]0); $w.Write([int]0); $w.Write([int]0)
    for ($y = $px - 1; $y -ge 0; $y--) { $w.Write($pixels, $y * $stride, $stride) }
    $w.Write((New-Object byte[] ($maskStride * $px)))
    $w.Flush(); ,$stream.ToArray()
}

function Write-Ico([string]$path, [int[]]$sizes) {
    $images = foreach ($px in $sizes) {
        $bitmap = Render $px
        [pscustomobject]@{ Size = $px; Data = $(if ($px -ge 256) { Png-Bytes $bitmap } else { Dib-Bytes $bitmap $px }) }
    }
    $stream = [System.IO.File]::Create($path)
    $w = New-Object System.IO.BinaryWriter $stream
    $w.Write([int16]0); $w.Write([int16]1); $w.Write([int16]$images.Count)
    $offset = 6 + 16 * $images.Count
    foreach ($image in $images) {
        $edge = if ($image.Size -ge 256) { 0 } else { $image.Size }
        $w.Write([byte]$edge); $w.Write([byte]$edge); $w.Write([byte]0); $w.Write([byte]0)
        $w.Write([int16]1); $w.Write([int16]32); $w.Write([int]$image.Data.Length); $w.Write([int]$offset)
        $offset += $image.Data.Length
    }
    foreach ($image in $images) { $w.Write([byte[]]$image.Data) }
    $w.Close()
}

function Write-Svg([string]$path) {
    $d = $Design; $c = [System.Globalization.CultureInfo]::InvariantCulture
    $stops = ($d.TileStops | ForEach-Object { '<stop offset="{0}" stop-color="{1}"/>' -f $_[1].ToString($c), $_[0] }) -join ''
    $corners = ($d.Corners | ForEach-Object { '<path d="{0}"/>' -f $_ }) -join ''
    $t = $d.Tile; $p = $d.Page
    $svg = @"
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 256 256" width="256" height="256">
  <title>SnipCanvas</title>
  <defs>
    <linearGradient id="tile" x1="0" y1="0" x2="1" y2="1">$stops</linearGradient>
    <linearGradient id="sheen" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#fff" stop-opacity="0.27"/><stop offset="0.5" stop-color="#fff" stop-opacity="0"/></linearGradient>
  </defs>
  <rect x="$($t[0])" y="$($t[1])" width="$($t[2])" height="$($t[3])" rx="$($d.TileRadius)" fill="url(#tile)"/>
  <rect x="$($t[0])" y="$($t[1])" width="$($t[2])" height="$($t[3])" rx="$($d.TileRadius)" fill="url(#sheen)"/>
  <g fill="none" stroke="$($d.CornerInk)" stroke-width="$($d.CornerWidth)" stroke-linecap="round" stroke-linejoin="round">$corners</g>
  <g transform="rotate($($d.PageTilt.ToString($c)) $($d.PagePivot[0]) $($d.PagePivot[1]))">
    <rect x="$($p[0])" y="$($p[1] + $d.PageShadow)" width="$($p[2])" height="$($p[3])" rx="$($d.PageRadius)" fill="#1A1170" fill-opacity="0.27"/>
    <rect x="$($p[0])" y="$($p[1])" width="$($p[2])" height="$($p[3])" rx="$($d.PageRadius)" fill="#fff"/>
    <path d="$($d.Arrow)" fill="none" stroke="$($d.ArrowInk)" stroke-width="$($d.ArrowWidth)" stroke-linecap="round" stroke-linejoin="round"/>
  </g>
</svg>
"@
    [System.IO.File]::WriteAllText($path, $svg, (New-Object System.Text.UTF8Encoding $false))
}

# ---- Installer artwork (Inno Setup wizard): a large side panel and a small header image, each at 1x and 2x ------
function Draw-WizardLarge($dc) {   # 164 x 314 units
    $dc.DrawRectangle((Gradient 0.35 1 @(@('#2E2696', 0.0), @('#1A1468', 0.55), @('#0E0A3A', 1.0))), $null, (New-Object System.Windows.Rect 0, 0, 164, 314))
    $glow = New-Object System.Windows.Media.RadialGradientBrush (Col '#7B6BFF' 120), (Col '#7B6BFF' 0)
    $dc.DrawEllipse($glow, $null, (New-Object System.Windows.Point 82, 112), 120, 120)
    $dc.PushTransform((New-Object System.Windows.Media.TranslateTransform 26, 56))
    $dc.PushTransform((New-Object System.Windows.Media.ScaleTransform (112 / 256.0), (112 / 256.0)))
    Draw-Logo $dc 1.0
    $dc.Pop(); $dc.Pop()
    $face = New-Object System.Windows.Media.Typeface 'Segoe UI Semibold'
    foreach ($line in @(@('SnipCanvas', 21, '#FFFFFF', 204), @('Capture. Mark up. Share.', 10.5, '#B9B2FF', 236))) {
        $text = New-Object System.Windows.Media.FormattedText $line[0], ([System.Globalization.CultureInfo]::InvariantCulture), ([System.Windows.FlowDirection]::LeftToRight), $face, $line[1], (Fill $line[2]), 1.0
        $dc.DrawText($text, (New-Object System.Windows.Point ((164 - $text.Width) / 2), $line[3]))
    }
}
function Draw-WizardSmall($dc) {   # 55 x 55 units, on white like the wizard header
    $dc.DrawRectangle((Fill '#FFFFFF'), $null, (New-Object System.Windows.Rect 0, 0, 55, 55))
    $dc.PushTransform((New-Object System.Windows.Media.TranslateTransform 3.5, 3.5))
    $dc.PushTransform((New-Object System.Windows.Media.ScaleTransform (48 / 256.0), (48 / 256.0)))
    Draw-Logo $dc 1.15
    $dc.Pop(); $dc.Pop()
}
function Write-Wizard([string]$path, [string]$kind, [int]$scale) {
    $width = if ($kind -eq 'large') { 164 } else { 55 }; $height = if ($kind -eq 'large') { 314 } else { 55 }
    $visual = New-Object System.Windows.Media.DrawingVisual
    $dc = $visual.RenderOpen()
    $dc.PushTransform((New-Object System.Windows.Media.ScaleTransform $scale, $scale))
    if ($kind -eq 'large') { Draw-WizardLarge $dc } else { Draw-WizardSmall $dc }
    $dc.Pop(); $dc.Close()
    $bitmap = New-Object System.Windows.Media.Imaging.RenderTargetBitmap ($width * $scale), ($height * $scale), 96, 96, ([System.Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    $flat = New-Object System.Windows.Media.Imaging.FormatConvertedBitmap $bitmap, ([System.Windows.Media.PixelFormats]::Bgr24), $null, 0
    $encoder = New-Object System.Windows.Media.Imaging.BmpBitmapEncoder
    $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($flat))
    $stream = [System.IO.File]::Create($path); $encoder.Save($stream); $stream.Close()
}

if ($Preview) {
    $sizes = 256, 128, 64, 48, 32, 24, 16
    $sheet = New-Object System.Windows.Media.DrawingVisual
    $dc = $sheet.RenderOpen()
    $dc.DrawRectangle((Fill '#F3F4F8'), $null, (New-Object System.Windows.Rect 0, 0, 520, 300))
    $dc.DrawRectangle((Fill '#14161D'), $null, (New-Object System.Windows.Rect 520, 0, 520, 300))
    foreach ($half in 0, 520) {
        $x = 16 + $half
        foreach ($px in $sizes) {
            $shown = if ($px -ge 128) { $px / 2 } else { $px }
            $dc.DrawImage((Render $px), (New-Object System.Windows.Rect $x, 20, $shown, $shown)); $x += $shown + 14
        }
        $dc.DrawImage((Render 256), (New-Object System.Windows.Rect (16 + $half), 110, 170, 170))
        $dc.DrawImage((Render 16), (New-Object System.Windows.Rect (210 + $half), 110, 96, 96))   # 16 px shown 6x, unfiltered
        $dc.DrawImage((Render 32), (New-Object System.Windows.Rect (320 + $half), 110, 96, 96))
    }
    $dc.Close()
    $sheetBitmap = New-Object System.Windows.Media.Imaging.RenderTargetBitmap 1040, 300, 96, 96, ([System.Windows.Media.PixelFormats]::Pbgra32)
    $sheetBitmap.Render($sheet)
    [System.IO.File]::WriteAllBytes((Join-Path $Out 'logo-preview.png'), (Png-Bytes $sheetBitmap))
    return
}

[System.IO.File]::WriteAllBytes((Join-Path $Out 'snipcanvas.png'), (Png-Bytes (Render 1024)))
Write-Ico (Join-Path $Out 'snipcanvas.ico') 16, 20, 24, 32, 40, 48, 64, 128, 256
Write-Svg (Join-Path $Out 'snipcanvas.svg')
'Wrote snipcanvas.svg, snipcanvas.png and snipcanvas.ico to ' + $Out
if ($Installer) {
    New-Item -ItemType Directory -Force $Installer | Out-Null
    Write-Wizard (Join-Path $Installer 'wizard-large.bmp') 'large' 1
    Write-Wizard (Join-Path $Installer 'wizard-large-2x.bmp') 'large' 2
    Write-Wizard (Join-Path $Installer 'wizard-small.bmp') 'small' 1
    Write-Wizard (Join-Path $Installer 'wizard-small-2x.bmp') 'small' 2
    'Wrote installer wizard images to ' + $Installer
}
