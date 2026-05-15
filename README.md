# Reminders for iCloud

A tiny Windows launcher that opens the iCloud Reminders web app
(<https://www.icloud.com/reminders>) in **Microsoft Edge "app mode"**
(`msedge.exe --app=...`) with its own isolated user profile.

You get a dedicated, taskbar-separate window for iCloud Reminders — but
the underlying browser is real Edge, so all the accessibility, keyboard
navigation, and screen-reader behaviour you already trust in Edge applies
1:1 here.

**Not affiliated with Apple Inc.** iCloud is a registered trademark of
Apple Inc. This project is an independent, open-source convenience
launcher for the public iCloud web app and does not modify, redistribute,
or reverse-engineer any Apple software.

## Why a launcher, not an embedded webview?

The first iteration of this project embedded iCloud Reminders in a
WebView2 control inside a WPF window. WebView2 *is* the Edge engine, but
iCloud's single-page app serves a different (and noticeably less
accessible) code path when it detects an embedded WebView2 host — arrow-
key navigation inside the reminders grid, in particular, silently breaks
even after aggressive UA / UA-CH / brand spoofing. Rather than chase an
undocumented detection vector forever, this launcher just *is* Edge: the
only reliable fix.

## What the launcher does

1. Locates `msedge.exe` via `HKLM/HKCU\…\App Paths\msedge.exe` and the
   standard install locations.
2. Creates a dedicated profile directory at
   `%LOCALAPPDATA%\RemindersForICloud\EdgeProfile`.
3. Spawns Edge as:
   ```
   msedge.exe --app=https://www.icloud.com/reminders
              --user-data-dir=%LOCALAPPDATA%\RemindersForICloud\EdgeProfile
              --no-first-run
              --no-default-browser-check
   ```
4. Exits. Edge owns the window from here on.

A short cross-process mutex around the launch coalesces fast double-clicks
so you don't end up with two parallel Edge windows on accident.

## Features

- **Native Edge window** — same chrome as a Progressive Web App: minimal
  title bar, no URL bar, taskbar entry distinct from regular Edge.
- **Persistent login** — your iCloud session lives in the dedicated profile
  folder and survives reboots. Apple's 2FA "trust this browser" cookie
  applies, so you re-authenticate roughly every 30 days.
- **Full Edge accessibility** — screen readers, keyboard navigation,
  high-contrast, dark mode all behave exactly as in standalone Edge.

## Requirements

- Windows 10 1809+ or Windows 11
- Microsoft Edge installed (the Edge Stable channel; preinstalled on
  Windows 11).
- .NET 10 Runtime — the released `.exe` ships self-contained, so end users
  do not need .NET installed.

## Building

```pwsh
dotnet build -c Release
dotnet run -c Release
```

To produce a self-contained, single-file executable:

```pwsh
dotnet publish -c Release -r win-x64 --self-contained `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

Output goes to `bin\Release\net10.0-windows\win-x64\publish\RemindersForICloud.exe`.

## Accessibility

Because we shell out to Edge, accessibility is whatever Edge supports —
which is everything Stefan's blind workflow already relies on: NVDA,
keyboard navigation in Apple's ARIA grids, dark mode, focus management.

## License

MIT — see [LICENSE](LICENSE).
