# Builds the three downloads for a GitHub release into publish/release. Each one unpacks into a
# DualStrikeMapper folder:
#   DualStrikeMapper-windows-x64.zip    DualStrikeMapper.exe (settings.json is saved next to it)
#   DualStrikeMapper-linux-x64.tar.gz   program + install.sh (adds icon and app-menu entry)
#   DualStrikeMapper-macos-arm64.zip    DualStrikeMapper.app (Apple Silicon)
# Needs PowerShell 7 (pwsh) for the tar/zip permission handling:
#   pwsh tools/make-release.ps1 -Version 1.0.0
param([string]$Version = "")
$ErrorActionPreference = 'Stop'

$root = Split-Path $PSScriptRoot
$app = Join-Path $root 'src/DualStrikeMapper.App'
$out = Join-Path $root 'publish/release'
New-Item -ItemType Directory -Force $out | Out-Null
Get-ChildItem $out -File | Remove-Item

foreach ($rid in 'win-x64', 'linux-x64', 'osx-arm64') {
    Write-Host "Building $rid..."
    $publishArgs = @($app, '-c', 'Release', '-r', $rid, '-o', (Join-Path $root "publish/$rid"), '-v', 'q', '-nologo')
    if ($Version) { $publishArgs += "-p:Version=$Version" }
    dotnet publish @publishArgs
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for $rid" }
}

$exec = 0x1ED   # rwxr-xr-x
$plain = 0x1A4  # rw-r--r--
function Text([string]$s) { [Text.Encoding]::UTF8.GetBytes($s.Replace("`r`n", "`n")) }

# ---- Zip helper that keeps Unix permissions (needed for Linux/macOS programs) ----

function New-Zip([string]$path, [array]$entries) {
    $zip = [IO.Compression.ZipFile]::Open($path, [IO.Compression.ZipArchiveMode]::Create)
    foreach ($e in $entries) {
        $entry = $zip.CreateEntry($e.Name, [IO.Compression.CompressionLevel]::Optimal)
        $entry.ExternalAttributes = (0x8000 -bor $e.Mode) -shl 16   # regular file + Unix permissions
        $s = $entry.Open(); $s.Write($e.Bytes, 0, $e.Bytes.Length); $s.Dispose()
    }
    $zip.Dispose()
}

# ---- Windows ----

New-Zip (Join-Path $out "DualStrikeMapper-windows-x64.zip") @(
    @{ Name = 'DualStrikeMapper/DualStrikeMapper.exe'; Bytes = [IO.File]::ReadAllBytes((Join-Path $root 'publish/win-x64/DualStrikeMapper.exe')); Mode = $exec }
)

# ---- Linux: tar.gz with Unix permissions kept, so the program and installer can run right away ----

$linux = Join-Path $root 'packaging/linux'
$files = @(
    @{ Name = 'DualStrikeMapper'; Bytes = [IO.File]::ReadAllBytes((Join-Path $root 'publish/linux-x64/DualStrikeMapper')); Mode = $exec },
    @{ Name = 'install.sh'; Bytes = [IO.File]::ReadAllBytes((Join-Path $linux 'install.sh')); Mode = $exec },
    @{ Name = 'dualstrikemapper.desktop'; Bytes = [IO.File]::ReadAllBytes((Join-Path $linux 'dualstrikemapper.desktop')); Mode = $plain }
)
Get-ChildItem $linux -Filter 'dualstrikemapper-*.png' | ForEach-Object {
    $files += @{ Name = $_.Name; Bytes = [IO.File]::ReadAllBytes($_.FullName); Mode = $plain }
}

$fs = [IO.File]::Create((Join-Path $out "DualStrikeMapper-linux-x64.tar.gz"))
$gz = [IO.Compression.GZipStream]::new($fs, [IO.Compression.CompressionLevel]::Optimal)
$tar = [System.Formats.Tar.TarWriter]::new($gz, [System.Formats.Tar.TarEntryFormat]::Pax, $false)
$dir = [System.Formats.Tar.PaxTarEntry]::new([System.Formats.Tar.TarEntryType]::Directory, 'DualStrikeMapper/')
$dir.Mode = [IO.UnixFileMode]$exec
$tar.WriteEntry($dir)
foreach ($f in $files) {
    $entry = [System.Formats.Tar.PaxTarEntry]::new([System.Formats.Tar.TarEntryType]::RegularFile, "DualStrikeMapper/$($f.Name)")
    $entry.Mode = [IO.UnixFileMode]$f.Mode
    $entry.ModificationTime = [DateTimeOffset]::Now
    $entry.DataStream = [IO.MemoryStream]::new($f.Bytes)
    $tar.WriteEntry($entry)
}
$tar.Dispose(); $gz.Dispose(); $fs.Dispose()

# ---- macOS: DualStrikeMapper.app with the executable bit stored so Finder can open it ----

$mac = Join-Path $root 'packaging/macos'
$plist = Get-Content (Join-Path $mac 'Info.plist') -Raw
if ($Version) {
    $plist = $plist -replace '(<key>CFBundleShortVersionString</key>\s*<string>)[^<]*', "`${1}$Version"
    $plist = $plist -replace '(<key>CFBundleVersion</key>\s*<string>)[^<]*', "`${1}$Version"
}
New-Zip (Join-Path $out "DualStrikeMapper-macos-arm64.zip") @(
    @{ Name = 'DualStrikeMapper/DualStrikeMapper.app/Contents/MacOS/DualStrikeMapper'; Bytes = [IO.File]::ReadAllBytes((Join-Path $root 'publish/osx-arm64/DualStrikeMapper')); Mode = $exec },
    @{ Name = 'DualStrikeMapper/DualStrikeMapper.app/Contents/Info.plist'; Bytes = (Text $plist); Mode = $plain },
    @{ Name = 'DualStrikeMapper/DualStrikeMapper.app/Contents/Resources/AppIcon.icns'; Bytes = [IO.File]::ReadAllBytes((Join-Path $mac 'AppIcon.icns')); Mode = $plain }
)

Write-Host "`nReady to upload (publish/release):"
Get-ChildItem $out -File | ForEach-Object { Write-Host ("  {0,6:N1} MB  {1}" -f ($_.Length / 1MB), $_.Name) }
