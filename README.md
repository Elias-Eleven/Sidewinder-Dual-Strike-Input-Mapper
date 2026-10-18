# SideWinder Dual Strike Input Mapper

Modern compatibility software and mapper for the Microsoft Sidewinder Dual Strike Gamepad

Runs on Windows, Linux and macOS.

- **Handle → mouse.** Inside the aim zone the cursor follows the handle; push past it and the cursor keeps moving.
- **↑ re-centres** the cursor. Hold it, bring the handle back to the middle, let go.
- **Click any button** in the window to change what it does. Changes save automatically.
- **‹ ›** at the bottom switches layouts (Desktop, Shooter, or your own copies).

## Build it yourself

You need the **.NET 10 SDK**: <https://dotnet.microsoft.com/download>. Check it with `dotnet --version`.

### Run while developing

```bash
dotnet run --project src/DualStrikeMapper.App
```

### Make a standalone program

One file that runs without .NET installed. Pick your platform:

```bash
dotnet publish src/DualStrikeMapper.App -c Release -r win-x64 -o publish/win-x64
```

```bash
dotnet publish src/DualStrikeMapper.App -c Release -r linux-x64 -o publish/linux-x64
```

```bash
dotnet publish src/DualStrikeMapper.App -c Release -r osx-arm64 -o publish/osx-arm64
```

Use `osx-x64` for Intel Macs. The result is `publish/<platform>/DualStrikeMapper(.exe)`.
`settings.json` is saved next to it, so the folder can live anywhere (keep it somewhere you can write to).

### Make the release downloads

Builds all three and packages them for a GitHub release into `publish/release` (needs PowerShell 7):

```bash
pwsh tools/make-release.ps1 -Version 1.0.0
```

You get `DualStrikeMapper-windows-x64.zip`, `…-linux-x64.tar.gz` (program + `install.sh`)
and `…-macos-arm64.zip` (`DualStrikeMapper.app`). Each unpacks into a `DualStrikeMapper` folder;
on Windows and Linux `settings.json` is saved in that folder, on macOS in
`~/Library/Application Support/DualStrikeMapper`. Attach the three files to the release.

### Add it to the app menu (Linux) or make a .app (macOS)

The Windows `.exe` already carries the icon. On the other two, one extra step adds the icon and launcher:

```bash
sh packaging/linux/install.sh publish/linux-x64/DualStrikeMapper
```

```bash
sh packaging/macos/make-app.sh publish/osx-arm64/DualStrikeMapper
```

Linux installs to `~/.local/opt/DualStrikeMapper` with a menu entry. macOS makes `DualStrikeMapper.app`
next to the binary; it isn't signed, so open it the first time with right-click → Open.

### Editors

- **VS Code:** install the *C# Dev Kit* extension, open this folder, press F5.
- **Visual Studio 2026** or **JetBrains Rider:** open `DualStrikeMapper.slnx`.
  Rider and the *Avalonia for VS Code / VS* extensions add a live XAML preview.

## Platform notes

- **macOS:** allow the app under *System Settings → Privacy & Security → Accessibility*, or it can't move the mouse.
- **Linux:** on Wayland the app uses a virtual input device and needs write access to `/dev/uinput`
  (usually: add yourself to the `input` group, or add a udev rule). "Re-centre cursor" can't work on Wayland.
- Close **DualStrikeMouse** first, otherwise both apps move the cursor. The app warns you if it's running.

## Project layout

| Folder | What's in it |
| --- | --- |
| `src/DualStrikeMapper.Core` | Controller reading (SDL3), mouse engine, button actions, profiles, settings |
| `src/DualStrikeMapper.App` | The window (Avalonia): controller picture, editors, tray icon |
| `src/DualStrikeMapper.Probe` | Console tool that prints raw controller input, for testing |
| `packaging` | Linux launcher + icons, macOS app bundle files |
| `tools` | Scripts that remake the icons and the controller photo |

### Changing the pictures

- **App icon:** edit `Logo.tiff`, then run the script below. It remakes the Windows `.ico`, the macOS
  `.icns`, and the Linux PNGs.
- **Controller photo:** replace `microsoft-sidewinder-dualstrike.tiff` (square, transparent background),
  then run the second script. If the framing changes, move the button positions in `Views/ControllerCanvas.cs`.

```bash
powershell -ExecutionPolicy Bypass -File tools/make-icons.ps1
```

```bash
powershell -ExecutionPolicy Bypass -File tools/make-photo.ps1
```

Both scripts need Windows. Rebuild the app afterwards.
