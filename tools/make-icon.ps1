# Rigenera MultiCursor.ico (due frecce: una arancione davanti, una bianca dietro).
# Uso:  powershell -ExecutionPolicy Bypass -File tools\make-icon.ps1
# -PreviewPng <file> salva anche un'anteprima ingrandita su sfondo chiaro e scuro.
param([string]$OutIco = (Join-Path $PSScriptRoot '..\MultiCursor.ico'), [string]$PreviewPng)
Add-Type -AssemblyName System.Drawing

function Draw-Icon([int]$s) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::Transparent)
    $k = $s / 24.0
    $xy = 0,0, 0,16, 4,12, 7,19, 9,18, 6,11, 11,11
    # freccia dietro (bianca) e freccia davanti (arancione)
    foreach ($a in @(@(11.5, 4.0, 'White'), @(2.0, 1.0, 'OrangeRed'))) {
        $pts = New-Object 'System.Drawing.PointF[]' 7
        for ($i = 0; $i -lt 7; $i++) {
            $pts[$i] = New-Object System.Drawing.PointF ([single](($a[0] + $xy[2*$i]) * $k)), ([single](($a[1] + $xy[2*$i+1]) * $k))
        }
        $brush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromName($a[2]))
        $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::Black), ([single][Math]::Max(1.0, $k * 1.1))
        $pen.LineJoin = 'Round'
        $g.FillPolygon($brush, $pts)
        $g.DrawPolygon($pen, $pts)
    }
    $g.Dispose()
    return $bmp
}

# Voce ICO in formato DIB a 32 bit (compatibile ovunque): header, pixel BGRA dal basso, maschera AND vuota.
function Get-DibBytes($bmp) {
    $s = $bmp.Width
    $ms = New-Object System.IO.MemoryStream
    $w = New-Object System.IO.BinaryWriter $ms
    $w.Write([int]40); $w.Write([int]$s); $w.Write([int]($s * 2)); $w.Write([int16]1); $w.Write([int16]32)
    $w.Write([int]0); $w.Write([int]0); $w.Write([int]0); $w.Write([int]0); $w.Write([int]0); $w.Write([int]0)
    for ($y = $s - 1; $y -ge 0; $y--) {
        for ($x = 0; $x -lt $s; $x++) { $w.Write([int]$bmp.GetPixel($x, $y).ToArgb()) }
    }
    $maskRow = [int]([Math]::Ceiling($s / 32.0)) * 4
    $w.Write((New-Object byte[] ($maskRow * $s)))
    $w.Flush()
    return ,$ms.ToArray()
}

$sizes = 16, 20, 24, 32, 48, 64
$images = @()
foreach ($s in $sizes) { $b = Draw-Icon $s; $images += ,(Get-DibBytes $b); $b.Dispose() }

$fs = [System.IO.File]::Create($OutIco)
$w = New-Object System.IO.BinaryWriter $fs
$w.Write([int16]0); $w.Write([int16]1); $w.Write([int16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $w.Write([byte]$sizes[$i]); $w.Write([byte]$sizes[$i]); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([int16]1); $w.Write([int16]32); $w.Write([int]$images[$i].Length); $w.Write([int]$offset)
    $offset += $images[$i].Length
}
foreach ($img in $images) { $w.Write($img) }
$w.Close()
"ico bytes: $((Get-Item $OutIco).Length)"
if (-not $PreviewPng) { return }

# Anteprima: 16, 32 e 64 px ingranditi su sfondo chiaro e scuro
$prev = New-Object System.Drawing.Bitmap 520, 280
$g = [System.Drawing.Graphics]::FromImage($prev)
$g.InterpolationMode = 'NearestNeighbor'; $g.PixelOffsetMode = 'Half'
$g.Clear([System.Drawing.Color]::FromArgb(240, 240, 240))
$g.FillRectangle([System.Drawing.Brushes]::DimGray, 260, 0, 260, 280)
foreach ($col in 0, 260) {
    $b = Draw-Icon 64; $g.DrawImage($b, $col + 10, 10, 128, 128)
    $b = Draw-Icon 32; $g.DrawImage($b, $col + 150, 10, 96, 96)
    $b = Draw-Icon 16; $g.DrawImage($b, $col + 150, 120, 96, 96)
    $b = Draw-Icon 16; $g.DrawImage($b, $col + 10, 180, 16, 16)
    $b = Draw-Icon 32; $g.DrawImage($b, $col + 40, 180, 32, 32)
}
$g.Dispose()
$prev.Save($PreviewPng, [System.Drawing.Imaging.ImageFormat]::Png)
