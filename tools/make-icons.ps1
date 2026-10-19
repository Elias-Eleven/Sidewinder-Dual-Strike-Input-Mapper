# Makes the app icons from Logo.tiff: Windows .ico, macOS .icns, Linux PNGs. The light outline
# that keeps the dark logo visible on dark taskbars is part of Logo.tiff. Run from anywhere:
#   powershell -ExecutionPolicy Bypass -File tools/make-icons.ps1
param(
    [string]$SrcPath = (Join-Path $PSScriptRoot '..\Logo.tiff'),
    [string]$AssetsDir = (Join-Path $PSScriptRoot '..\src\DualStrikeMapper.App\Assets'),
    [string]$LinuxDir = (Join-Path $PSScriptRoot '..\packaging\linux'),
    [string]$MacDir = (Join-Path $PSScriptRoot '..\packaging\macos'),
    [string]$PreviewDir = $env:TEMP
)
Add-Type -AssemblyName System.Drawing
$logo = [System.Drawing.Image]::FromFile((Resolve-Path $SrcPath))

function Render([int]$size) {
  $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.InterpolationMode = 'HighQualityBicubic'; $g.SmoothingMode = 'HighQuality'; $g.PixelOffsetMode = 'HighQuality'
  $g.Clear([System.Drawing.Color]::Transparent)
  # Fit the logo (a bit wider than tall) into the square and keep its shape.
  $margin = [Math]::Floor($size / 32)
  $scale = [Math]::Min(($size - 2 * $margin) / $logo.Width, ($size - 2 * $margin) / $logo.Height)
  $w = $logo.Width * $scale; $h = $logo.Height * $scale
  $x = ($size - $w) / 2; $y = ($size - $h) / 2
  $g.DrawImage($logo, [single]$x, [single]$y, [single]$w, [single]$h)
  $g.Dispose()
  $ms = New-Object System.IO.MemoryStream
  $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
  return ,$ms.ToArray()
}

$png = @{}
foreach ($s in 16, 20, 24, 32, 40, 48, 64, 128, 256, 512, 1024) { $png[$s] = Render $s }
$logo.Dispose()

# Windows .ico (PNG-compressed entries, up to 256).
$icoSizes = 16, 20, 24, 32, 40, 48, 64, 128, 256
$out = New-Object System.IO.MemoryStream; $bw = New-Object System.IO.BinaryWriter $out
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$icoSizes.Count)
$offset = 6 + 16 * $icoSizes.Count
foreach ($s in $icoSizes) {
  $d = if ($s -ge 256) { 0 } else { $s }
  $bw.Write([byte]$d); $bw.Write([byte]$d); $bw.Write([byte]0); $bw.Write([byte]0)
  $bw.Write([uint16]1); $bw.Write([uint16]32); $bw.Write([uint32]$png[$s].Length); $bw.Write([uint32]$offset)
  $offset += $png[$s].Length
}
foreach ($s in $icoSizes) { $bw.Write([byte[]]$png[$s]) }
$bw.Flush(); [System.IO.File]::WriteAllBytes((Join-Path $AssetsDir 'app.ico'), $out.ToArray())

# PNG for the app itself (window icon on Linux/macOS) and for Linux desktop launchers.
[System.IO.File]::WriteAllBytes((Join-Path $AssetsDir 'app.png'), $png[256])
foreach ($s in 16, 32, 48, 64, 128, 256, 512) {
  [System.IO.File]::WriteAllBytes((Join-Path $LinuxDir "dualstrikemapper-$s.png"), $png[$s])
}

# macOS .icns: 'icns' + total length, then (type, length incl. 8-byte header, PNG data) entries. Big-endian.
function BE([uint32]$v) { $b = [BitConverter]::GetBytes($v); [Array]::Reverse($b); return ,$b }
$entries = @(('icp4', 16), ('icp5', 32), ('icp6', 64), ('ic07', 128), ('ic08', 256), ('ic09', 512), ('ic10', 1024))
$body = New-Object System.IO.MemoryStream
foreach ($e in $entries) {
  $data = $png[$e[1]]
  $body.Write([Text.Encoding]::ASCII.GetBytes($e[0]), 0, 4)
  $len = BE ([uint32]($data.Length + 8)); $body.Write($len, 0, 4)
  $body.Write($data, 0, $data.Length)
}
$icns = New-Object System.IO.MemoryStream
$icns.Write([Text.Encoding]::ASCII.GetBytes('icns'), 0, 4)
$total = BE ([uint32]($body.Length + 8)); $icns.Write($total, 0, 4)
$body.WriteTo($icns)
[System.IO.File]::WriteAllBytes((Join-Path $MacDir 'AppIcon.icns'), $icns.ToArray())

# Preview: 16/32/48 px on dark and light backgrounds, drawn 2x with hard pixels.
$strip = New-Object System.Drawing.Bitmap 360, 240
$g = [System.Drawing.Graphics]::FromImage($strip); $g.InterpolationMode = 'NearestNeighbor'; $g.PixelOffsetMode = 'Half'
$g.FillRectangle((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(32, 32, 32))), 0, 0, 360, 120)
$g.FillRectangle((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(243, 243, 243))), 0, 120, 360, 120)
$x = 20
foreach ($s in 16, 32, 48) {
  $img = [System.Drawing.Image]::FromStream((New-Object System.IO.MemoryStream (,$png[$s])))
  $z = $s * 2
  $g.DrawImage($img, $x, [single](60 - $z / 2), $z, $z); $g.DrawImage($img, $x, [single](180 - $z / 2), $z, $z)
  $x += $z + 40; $img.Dispose()
}
$g.Dispose(); $strip.Save((Join-Path $PreviewDir 'icon_preview.png'), [System.Drawing.Imaging.ImageFormat]::Png)
"Made app.ico, app.png, AppIcon.icns and Linux PNGs. Preview: $(Join-Path $PreviewDir 'icon_preview.png')"
