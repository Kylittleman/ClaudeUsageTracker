# Claude Usage Tracker (Windows)

[![Download for Windows](https://img.shields.io/badge/Download-ClaudeUsageTracker.exe-blue?style=for-the-badge&logo=windows)](https://github.com/Kylittleman/ClaudeUsageTracker/releases/latest/download/ClaudeUsageTracker.exe)

*Single exe, no installer. See [Quick start](#quick-start) below.*

A Windows system-tray app that shows your live claude.ai usage - 5-hour and 7-day/weekly
limits, plus 7-day Opus usage - without opening the Settings > Usage page in a browser.

Inspired by two macOS menu-bar apps:
- [Usage4Claude](https://github.com/f-is-h/Usage4Claude)
- [Claude-Usage-Tracker](https://github.com/hamed-elfayome/Claude-Usage-Tracker)

Built with C# / .NET 8 (WPF), [H.NotifyIcon.Wpf](https://github.com/HavenDV/H.NotifyIcon) for
the tray icon, and [WebView2](https://developer.microsoft.com/microsoft-edge/webview2/) (a
real embedded Edge/Chromium engine, built into Windows 11) for talking to claude.ai.

## Quick start

1. **[Download ClaudeUsageTracker.exe](https://github.com/Kylittleman/ClaudeUsageTracker/releases/latest/download/ClaudeUsageTracker.exe)**
   and run it. It's unsigned, so Windows SmartScreen will likely show "Windows protected
   your PC" - click **More info -> Run anyway** (this is normal for a small indie tool
   without a paid code-signing certificate).
2. It opens Settings automatically on first run. Click **Log in with claude.ai** - a window
   opens, sign in to claude.ai like you normally would, and it closes itself once you're in.
3. Pick your organization (auto-loaded after login) and click **Save**.
4. It drops into your system tray showing your live 5-hour usage %. Click the icon any time
   for the full breakdown.

## How it works

Neither this app nor the macOS originals read any local Claude Code files. They call
claude.ai's own web API directly:

- `GET https://claude.ai/api/organizations` - discovers your organization(s)
- `GET https://claude.ai/api/organizations/{org_id}/usage` - returns `five_hour`,
  `seven_day`, and `seven_day_opus` utilization percentages

**Why WebView2 instead of a plain HTTP client:** those endpoints sit behind Cloudflare bot
protection that blocks bare HTTP requests outright - even ones carrying a valid, correctly
copied session cookie - because it can't run the JS challenge a real browser solves
automatically. So this app logs in and fetches data through an actual embedded Edge engine
(WebView2) rather than raw HTTP calls, which is presumably what the macOS apps' "embedded
browser login" option does too. Your login session is stored in WebView2's own local browser
profile (`%LOCALAPPDATA%\ClaudeUsageTracker\WebView2`), the same way a real browser stores it
- not something this app manages or encrypts itself.

## Features

- Tray icon shows your current 5-hour usage % directly (green/amber/red).
- Click the tray icon for a popup with all three usage bars and reset countdowns.
- Adaptive refresh: faster while you're actively checking, slower when idle.
- Optional Windows notification when usage crosses 80% / 95%.
- Optional auto-start at Windows login (adds a `HKCU...\Run` entry - no installer needed).

## Building from source

Requires the .NET 8 SDK. The WebView2 Runtime must be present on the machine (ships with
Windows 11 by default).

```
dotnet build
dotnet run
```

## Publishing a standalone exe

```
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

Produces the same kind of single self-contained `ClaudeUsageTracker.exe` attached to
[Releases](https://github.com/Kylittleman/ClaudeUsageTracker/releases) - runs without the
.NET SDK/runtime installed. Not checked into this repo; build it yourself or grab the release.

## Privacy

This app only talks to `claude.ai` (through the embedded browser component) and nowhere else.
Nothing is sent anywhere else; there is no telemetry.
