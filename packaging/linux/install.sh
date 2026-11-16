#!/bin/sh
# Installs Sidewinder Dual Strike Input Mapper for the current user, with its icon and an app-menu entry.
# From the release download: extract it, then run  sh install.sh
# From the source (after `dotnet publish ... -r linux-x64 -o publish/linux-x64`):
#   sh packaging/linux/install.sh "publish/linux-x64/Sidewinder Dual Strike Input Mapper"
set -e

name="Sidewinder Dual Strike Input Mapper"
command_name="sidewinder-dual-strike-input-mapper"

here=$(cd "$(dirname "$0")" && pwd)
# The release download has the program right next to this script.
default="$here/$name"
[ -f "$default" ] || default="$here/../../publish/linux-x64/$name"
binary=${1:-"$default"}
[ -f "$binary" ] || { echo "Build it first: dotnet publish src/DualStrikeMapper.App -c Release -r linux-x64 -o publish/linux-x64"; exit 1; }

# The app keeps settings.json next to itself, so give it its own writable folder.
# (The folder keeps its old name so settings from earlier installs carry over.)
app_dir="$HOME/.local/opt/DualStrikeMapper"
mkdir -p "$app_dir" "$HOME/.local/bin" "$HOME/.local/share/applications"
cp "$binary" "$app_dir/$name"
chmod +x "$app_dir/$name"
ln -sf "$app_dir/$name" "$HOME/.local/bin/$command_name"

# Clean up the program and command from installs made before the rename.
rm -f "$app_dir/DualStrikeMapper"
[ -L "$HOME/.local/bin/DualStrikeMapper" ] && rm -f "$HOME/.local/bin/DualStrikeMapper"

for size in 16 32 48 64 128 256 512; do
    dir="$HOME/.local/share/icons/hicolor/${size}x${size}/apps"
    mkdir -p "$dir"
    cp "$here/dualstrikemapper-$size.png" "$dir/dualstrikemapper.png"
done

sed "s|^Exec=.*|Exec=\"$app_dir/$name\"|" "$here/dualstrikemapper.desktop" \
    > "$HOME/.local/share/applications/dualstrikemapper.desktop"

command -v update-desktop-database >/dev/null && update-desktop-database "$HOME/.local/share/applications" || true
command -v gtk-update-icon-cache >/dev/null && gtk-update-icon-cache -q "$HOME/.local/share/icons/hicolor" || true
echo "Installed. Find $name in your app menu, or run $command_name in a terminal."
