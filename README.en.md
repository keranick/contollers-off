# Controllers-OFF

[Русский](README.md) · **English**

> This app was built to make my own life easier while playing **DaggerFall Online**.
> Whenever a controller with potentiometers is plugged into the PC, **DFO** shows a phantom cursor.
> The problem exists in both the browser and the desktop build.
> I got tired of crawling under the desk to unplug my wheel, so I wrote this app.

## Screenshot

![ControllersOff panel](docs/screenshot.png)

## Features

- Lives in the tray: left-clicking the icon opens a compact panel next to the tray, another click or Esc closes it.
- The list of connected HID devices (wheels, shifters, joysticks, gamepads, custom vendor collections) is grouped by type, each row has its own on/off switch.
- "Keyboard & mouse" turns everything off except input devices. The button is disabled when there is nothing to turn off.
- "Restore initial state" turns back on the devices the app switched off in this or a previous session.
- Right-click on the tray icon: open the panel, "Enable all" (panic switch that turns on everything the app has ever left off), exit.
- Settings: theme (system/dark/light), autostart, language (RU/EN), picking the main keyboard and mouse when there is more than one.
- Enabling and disabling devices requires administrator rights — the app asks for UAC itself.
- Windows stops reporting switched-off devices in enumeration, so the app remembers them and restores them on exit.

## Requirements

- Windows 10/11 x64.
- .NET 8 Desktop Runtime (the portable build is framework-dependent, ~6 MB).

## Portable build

1. Unpack the archive.
2. Run `ControllersOff.exe`. No installation needed, settings live in `%LOCALAPPDATA%\ControllersOff`.
3. The app starts in the tray. To have it come up with the system, turn on autostart in the settings.

The diagnostic log is written to `%LOCALAPPDATA%\ControllersOff\toggle.log`.

## Building from source

```
dotnet build ControllersOff.sln
dotnet test  ControllersOff.sln
dotnet publish src/ControllersOff/ControllersOff.csproj -p:PublishProfile=win-x64
```

The ready exe lands in `artifacts/portable`. The `win-x64` profile produces a single-file framework-dependent build; set `SelfContained=true` for a fully standalone one.
