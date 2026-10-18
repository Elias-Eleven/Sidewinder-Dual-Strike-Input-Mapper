#!/bin/sh
# Wraps the published macOS binary in DualStrikeMapper.app with its icon.
# Usage (after `dotnet publish ... -r osx-arm64 -o publish/osx-arm64`):
#   sh packaging/macos/make-app.sh publish/osx-arm64/DualStrikeMapper
set -e

here=$(cd "$(dirname "$0")" && pwd)
binary=${1:-"$here/../../publish/osx-arm64/DualStrikeMapper"}
[ -f "$binary" ] || { echo "Build it first: dotnet publish src/DualStrikeMapper.App -c Release -r osx-arm64 -o publish/osx-arm64"; exit 1; }

out="$(dirname "$binary")/DualStrikeMapper.app"
rm -rf "$out"
mkdir -p "$out/Contents/MacOS" "$out/Contents/Resources"
cp "$binary" "$out/Contents/MacOS/DualStrikeMapper"
chmod +x "$out/Contents/MacOS/DualStrikeMapper"
cp "$here/Info.plist" "$out/Contents/Info.plist"
cp "$here/AppIcon.icns" "$out/Contents/Resources/AppIcon.icns"

# Not signed: the first time, right-click the app and choose Open.
# Ad-hoc signing keeps macOS happier on Apple Silicon.
command -v codesign >/dev/null && codesign --force --deep --sign - "$out" || true
echo "Made $out. Drag it to Applications."
