# Claude Usage Tracker (Windows)

A Windows system-tray app that shows your live claude.ai usage - 5-hour and 7-day/weekly
limits, plus 7-day Opus usage - without opening the Settings > Usage page in a browser.

Inspired by two macOS menu-bar apps:
- [Usage4Claude](https://github.com/f-is-h/Usage4Claude)
- [Claude-Usage-Tracker](https://github.com/hamed-elfayome/Claude-Usage-Tracker)

Built with C# / .NET 8 (WPF) and [H.NotifyIcon.Wpf](https://github.com/HavenDV/H.NotifyIcon)
for the tray icon.

## How it works

Neither this app nor the macOS originals read any local Claude Code files. They call
claude.ai's own web API directly:

- `GET https://claude.ai/api/organizations` - discovers your organization(s)
- `GET https://claude.ai/api/organizations/{org_id}/usage` - returns `five_hour`,
  `seven_day`, and `seven_day_opus` utilization percentages

Both are authenticated with the `sessionKey` cookie claude.ai sets when you log in through
a browser - the same credential the site itself uses, not a separate API key.

## Getting your session key

1. Open [claude.ai](https://claude.ai) in your browser and sign in.
2. Open DevTools (F12) -> Application tab -> Cookies -> `https://claude.ai`.
3. Copy the value of the `sessionKey` cookie (starts with `sk-ant-sid01-...`).
4. Paste it into this app's Settings window, click **Test & Load Organizations**, pick your
   org, and click **Save**.

The key is encrypted at rest with Windows DPAPI (`ProtectedData`, `CurrentUser` scope) in
`%APPDATA%\ClaudeUsageTracker\config.json` - readable only by your Windows user account on
this machine, the same trust boundary the macOS apps get from Keychain.

## Features

- Tray icon shows your current 5-hour usage % directly (green/amber/red).
- Click the tray icon for a popup with all three usage bars and reset countdowns.
- Adaptive refresh: faster while you're actively checking, slower when idle.
- Optional Windows notification when usage crosses 80% / 95%.
- Optional auto-start at Windows login (adds a `HKCU...\Run` entry - no installer needed).

## Building

Requires the .NET 8 SDK.

```
dotnet build
dotnet run
```

## Publishing a standalone exe

```
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

Produces a single self-contained `ClaudeUsageTracker.exe` in `publish/` that runs without
the .NET SDK/runtime installed (not checked into this repo - build it yourself).

## Privacy

Your session key never leaves your machine except in direct HTTPS calls to `claude.ai`.
Nothing is sent anywhere else; there is no telemetry.
