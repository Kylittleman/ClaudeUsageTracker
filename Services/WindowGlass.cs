using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace ClaudeUsageTracker.Services;

/// <summary>
/// Applies real Windows acrylic blur-behind to a WPF window (the frosted-glass effect seen
/// throughout Windows 11) and detects whether the system is in light or dark mode, so windows
/// can match the user's Windows theme instead of hardcoding one look.
/// </summary>
public static class WindowGlass
{
    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public int AccentState;
        public int AccentFlags;
        public int GradientColor;
        public int AnimationId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData
    {
        public int Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }

    private const int AccentEnableAcrylicBlurBehind = 4;
    private const int WcaAccentPolicy = 19;

    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

    /// <summary>Enables acrylic blur-behind, tinted with the given color/opacity (0-255).</summary>
    public static void EnableAcrylic(Window window, Color tintColor, byte opacity)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;

        var gradientColor = (opacity << 24) | (tintColor.B << 16) | (tintColor.G << 8) | tintColor.R;
        var accent = new AccentPolicy
        {
            AccentState = AccentEnableAcrylicBlurBehind,
            GradientColor = gradientColor,
        };

        var accentSize = Marshal.SizeOf(accent);
        var accentPtr = Marshal.AllocHGlobal(accentSize);
        try
        {
            Marshal.StructureToPtr(accent, accentPtr, false);
            var data = new WindowCompositionAttributeData
            {
                Attribute = WcaAccentPolicy,
                SizeOfData = accentSize,
                Data = accentPtr,
            };
            SetWindowCompositionAttribute(hwnd, ref data);
        }
        finally
        {
            Marshal.FreeHGlobal(accentPtr);
        }
    }

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateRoundRectRgn(int left, int top, int right, int bottom, int cellWidth, int cellHeight);

    [DllImport("user32.dll")]
    private static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);

    /// <summary>
    /// Clips the window's actual OS-level shape to a rounded rectangle, so nothing - not even
    /// DWM's acrylic blur-behind, which otherwise fills the whole rectangular window regardless
    /// of WPF's own transparent regions - renders outside the rounded card.
    /// </summary>
    public static void ApplyRoundedCorners(Window window, double width, double height, double cornerRadius)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;

        var source = PresentationSource.FromVisual(window);
        var dpiScale = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;

        var w = (int)Math.Round(width * dpiScale);
        var h = (int)Math.Round(height * dpiScale);
        var d = (int)Math.Round(cornerRadius * dpiScale * 2);

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
