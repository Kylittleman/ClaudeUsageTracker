using System.IO;
using Microsoft.Web.WebView2.Core;

namespace ClaudeUsageTracker.Services;

/// <summary>
/// A single shared CoreWebView2Environment (and thus a single cookie/profile store) used by
/// both the login window and the hidden background fetcher, so logging in once in the visible
/// window is immediately usable by the hidden one.
/// </summary>
public static class BrowserEnvironment
{
    private static CoreWebView2Environment? _environment;
    private static Task<CoreWebView2Environment>? _initTask;

    public static Task<CoreWebView2Environment> GetAsync()
    {
        _initTask ??= CreateAsync();
        return _initTask;
    }

    private static async Task<CoreWebView2Environment> CreateAsync()
    {
        var userDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ClaudeUsageTracker", "WebView2");
        Directory.CreateDirectory(userDataFolder);

        _environment = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataFolder);
        return _environment;
    }
}
