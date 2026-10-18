#!/bin/sh
# Installs SideWinder Dual Strike Input Mapper for the current user, with its icon and an app-menu entry.
# Usage (after `dotnet publish ... -r linux-x64 -o publish/linux-x64`):
#   sh packaging/linux/install.sh publish/linux-x64/DualStrikeMapper
set -e

here=$(cd "$(dirname "$0")" && pwd)
binary=${1:-"$here/../../publish/linux-x64/DualStrikeMapper"}
[ -f "$binary" ] || { echo "Build it first: dotnet publish src/DualStrikeMapper.App -c Release -r linux-x64 -o publish/linux-x64"; exit 1; }

# The app keeps settings.json next to itself, so give it its own writable folder.
app_dir="$HOME/.local/opt/DualStrikeMapper"
mkdir -p "$app_dir" "$HOME/.local/bin" "$HOME/.local/share/applications"
cp "$binary" "$app_dir/DualStrikeMapper"
chmod +x "$app_dir/DualStrikeMapper"
ln -sf "$app_dir/DualStrikeMapper" "$HOME/.local/bin/DualStrikeMapper"

for size in 16 32 48 64 128 256 512; do
    dir="$HOME/.local/share/icons/hicolor/${size}x${size}/apps"
    mkdir -p "$dir"
    cp "$here/dualstrikemapper-$size.png" "$dir/dualstrikemapper.png"
done

sed "s|^Exec=.*|Exec=$app_dir/DualStrikeMapper|" "$here/dualstrikemapper.desktop" \
    > "$HOME/.local/share/applications/dualstrikemapper.desktop"

command -v update-desktop-database >/dev/null && update-desktop-database "$HOME/.local/share/applications" || true
command -v gtk-update-icon-cache >/dev/null && gtk-update-icon-cache -q "$HOME/.local/share/icons/hicolor" || true
echo "Installed. Find SideWinder Dual Strike Input Mapper in your app menu."
