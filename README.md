# SteamVR_tools

A small Windows tray tool that switches PICO Connect on and off in SteamVR, so you can move between a Pico headset and a Bigscreen Beyond without editing settings by hand.

日本語: [README/README.jp.md](README/README.jp.md)

## How it works

- **Pico mode**: PICO Connect is enabled (the `driver_pico` section has no `"enable": false`).
- **Beyond mode**: PICO Connect is disabled (`driver_pico` has `"enable": false` in `steamvr.vrsettings`).
- If SteamVR is running when you switch, a dialog offers **Restart** (close SteamVR, change the setting, start it again), **Do it manually** (change the setting only) or **Cancel**.
- Before every change the tool backs up `steamvr.vrsettings` (last 5 copies) and restores it if the write fails.
- The Steam install folder is detected from the registry. If that fails, you are asked to pick it once.

## Build and run

Requires the .NET 10 SDK on Windows.

```
dotnet build
dotnet test
dotnet run --project src/SteamVRTools.App
```

Settings and backups are stored under `%APPDATA%\SteamVRTools\`.

## Status

First stage (mode switching) is implemented. See [docs/design.md](docs/design.md) for the design and what is still unverified (for example, whether anything besides PICO Connect needs switching for the Beyond 2e).
