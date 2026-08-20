using Microsoft.Win32;

namespace ClaudeUsageTracker.Services;

/// <summary>Detects whether Windows is currently in light or dark mode, so windows can match the user's theme.</summary>
public static class WindowGlass
{
    /// <summary>True if Windows apps are currently set to light mode.</summary>
    public static bool IsLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            return value is int i && i != 0;
        }
        catch
        {
            return false;
        }
    }
}
