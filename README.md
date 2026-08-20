<div align="center">

# Claude Usage Tracker

[![Download for Windows](https://img.shields.io/badge/Download-ClaudeUsageTracker.exe-D97757?style=for-the-badge&logo=windows)](https://github.com/Kylittleman/ClaudeUsageTracker/releases/latest/download/ClaudeUsageTracker.exe)

<img src="docs/screenshot.png" alt="Claude Usage Tracker popup" width="320">

</div>

A Windows system-tray app that shows your live claude.ai usage - 5-hour and 7-day limits - at a
glance, without opening the Settings > Usage page in a browser. Inspired by
[Usage4Claude](https://github.com/f-is-h/Usage4Claude) and
[Claude-Usage-Tracker](https://github.com/hamed-elfayome/Claude-Usage-Tracker) for macOS.

## Is this safe?

Yes. It only talks to `claude.ai` - nothing else, no telemetry, no ads. It's fully open source;
the entire app is in this repo if you want to check.

Windows will show a **"Windows protected your PC"** warning the first time you run it. That's
because it's a free indie tool without a paid code-signing certificate ($100s/year), not a
malware detection - it's a publisher-trust signal, not a virus scan result. Click **More info**,
then **Run anyway**.

## How to use it

1. Download and run the exe above (click through the SmartScreen warning as described).
2. Settings opens automatically on first run. Click **Log in with claude.ai**, sign in like you
   normally would, and it closes itself once you're in.
3. Pick your organization and click **Save**.
4. Check your system tray (click the **^** arrow if it's hidden) - the icon shows your live
   5-hour usage %. Click it any time for the full breakdown, and use the pin icon to keep it
   open on screen permanently.

Settings also let you choose a solid or clear (see-through) style, refresh interval, usage
notifications, and launch-at-startup.

## Building from source

Requires the .NET 8 SDK.

```
dotnet build
dotnet run
```

Publish a standalone exe:

```
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

## License

[MIT](LICENSE)

---

<div align="center">

Created by **Kyler Hite** - Cybersecurity Student at BYU

</div>
