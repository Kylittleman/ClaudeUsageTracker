using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace ClaudeUsageTracker.Services;

/// <summary>
/// Clips a WPF window's true OS-level shape to a rounded rectangle, and detects whether the
/// system is in light or dark mode, so windows can match the user's Windows theme.
///
/// Deliberately does NOT use DWM's acrylic blur-behind (SetWindowCompositionAttribute /
/// ACCENT_ENABLE_ACRYLICBLURBEHIND): that legacy, undocumented API paints its blur+tint across
/// the *entire rectangular window* at the DWM compositor level, and does not respect
/// SetWindowRgn clipping on current Windows 11 builds - confirmed by testing, not assumed -
/// which is what caused the reported "gray square around the rounded card" bug. Relying purely
/// on WPF's own AllowsTransparency=True per-pixel alpha rendering (a well-supported, ordinary
/// WPF feature, not an OS/undocumented-API edge case) is what actually guarantees clean rounded
/// corners with nothing showing outside them.
/// </summary>
public static class WindowGlass
{
    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateRoundRectRgn(int left, int top, int right, int bottom, int cellWidth, int cellHeight);

    [DllImport("user32.dll")]
    private static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);

    /// <summary>
    /// Clips the window's actual OS-level shape to a rounded rectangle, as a defense-in-depth
    /// guarantee alongside WPF's own transparent-corner rendering.
    /// </summary>
    public static void ApplyRoundedCorners(Window window, double width, double height, double cornerRadius)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;

        var dpi = VisualTreeHelper.GetDpi(window);
        var scaleX = dpi.DpiScaleX;
        var scaleY = dpi.DpiScaleY;

        var w = (int)Math.Ceiling(width * scaleX);
        var h = (int)Math.Ceiling(height * scaleY);
        var radiusPhysical = cornerRadius * Math.Max(scaleX, scaleY);
        var d = (int)Math.Ceiling(radiusPhysical * 2);

        var region = CreateRoundRectRgn(0, 0, w, h, d, d);
        SetWindowRgn(hwnd, region, true);
    }

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
