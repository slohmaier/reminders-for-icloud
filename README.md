# Reminders for iCloud

A small, accessible Windows wrapper around the iCloud Reminders web app
(<https://www.icloud.com/reminders>). It opens the iCloud Reminders web
interface in a native Windows window with a stable title bar, dark mode, and
full screen reader (NVDA, Narrator, JAWS) support via WebView2.

**Not affiliated with Apple Inc.** iCloud is a registered trademark of Apple Inc.
This project is an independent, open-source convenience wrapper for the public
iCloud web app and does not modify, redistribute, or reverse-engineer any
Apple software.

## Why this exists

The iCloud Reminders web app already works well with screen readers, but
living inside a browser tab has downsides for daily use:

- it gets lost among other tabs,
- it doesn't have its own taskbar/Alt-Tab entry,
- a stray Ctrl+W kills it,
- browser chrome adds clutter to the screen reader's reading order.

This wrapper gives iCloud Reminders its own dedicated, single-instance
application window with the bare minimum of native chrome.

## Features

- Native Win32 window — proper UIA tree, screen-reader-friendly.
- Dark mode by default (via WebView2 `PreferredColorScheme`).
- Persistent login — cookies/storage live in
  `%LOCALAPPDATA%\RemindersForICloud\WebView2Data`.
- Single instance — launching twice just keeps the first window.
- External links open in your default browser instead of inside the app.
- Keyboard:
  - **F5** — reload
  - **Alt+Home** — back to `icloud.com/reminders`
  - All other keys go to the iCloud web app unchanged.

## Requirements

- Windows 10 1809+ or Windows 11
- [WebView2 Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/)
  (preinstalled on Windows 11)
- .NET 10 Runtime (for framework-dependent builds) — the released `.exe`
  ships self-contained, so end users do not need .NET installed.

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

The output lands in `bin\Release\net10.0-windows\win-x64\publish\`.

## Accessibility

This project treats screen-reader compatibility as a hard requirement, not a
polish item. The wrapper itself:

- exposes a meaningful UIA name on the window and the embedded WebView2 control,
- uses the native Windows title bar (no custom HTML chrome),
- forces dark mode in the embedded web app,
- does not intercept Tab, Shift+Tab, Alt+F4, or any other navigation key.

The accessibility of the iCloud Reminders interface itself is Apple's
responsibility. If you find regressions, please report them to Apple as well.

## License

MIT — see [LICENSE](LICENSE).
