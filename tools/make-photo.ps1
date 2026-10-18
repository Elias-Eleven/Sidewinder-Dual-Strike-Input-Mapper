# Turns the controller photo (TIFF, square, transparent background) into the PNG the app shows.
# Button positions in Views/ControllerCanvas.cs assume the same framing as the current photo.
# Run: powershell -ExecutionPolicy Bypass -File tools/make-photo.ps1
param(
    [string]$SrcPath = (Join-Path $PSScriptRoot '..\microsoft-sidewinder-dualstrike.tiff'),
    [string]$OutPath = (Join-Path $PSScriptRoot '..\src\DualStrikeMapper.App\Assets\dualstrike.png'),
    [int]$Size = 900
)
Add-Type -AssemblyName System.Drawing
$src = [System.Drawing.Image]::FromFile((Resolve-Path $SrcPath))
$bmp = New-Object System.Drawing.Bitmap $Size, $Size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.InterpolationMode = 'HighQualityBicubic'; $g.SmoothingMode = 'HighQuality'; $g.PixelOffsetMode = 'HighQuality'
$g.DrawImage($src, 0, 0, $Size, $Size)
$g.Dispose()
$bmp.Save($OutPath, [System.Drawing.Imaging.ImageFormat]::Png)
$src.Dispose(); $bmp.Dispose()
"Made $OutPath ($Size x $Size)"
