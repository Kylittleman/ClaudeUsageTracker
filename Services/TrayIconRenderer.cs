using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace ClaudeUsageTracker.Services;

/// <summary>Draws the current usage percentage directly onto the tray icon, so the number is visible without opening the popup.</summary>
public static class TrayIconRenderer
{
    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    public static Icon Render(int percent, bool isError = false)
    {
        const int size = 32;
        using var bitmap = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            var bg = isError ? Color.FromArgb(120, 120, 120) : ColorForPercent(percent);
            using var brush = new SolidBrush(bg);
            g.FillEllipse(brush, 1, 1, size - 2, size - 2);

            var text = isError ? "!" : percent.ToString();
            var fontSize = text.Length > 2 ? 11f : 14f;
            using var font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
            using var textBrush = new SolidBrush(Color.White);
            var textSize = g.MeasureString(text, font);
            g.DrawString(text, font, textBrush, (size - textSize.Width) / 2, (size - textSize.Height) / 2 - 1);
        }

        var hIcon = bitmap.GetHicon();
        return Icon.FromHandle(hIcon);
    }

    /// <summary>Must be called on the icon previously assigned to the tray after replacing it, to avoid leaking GDI handles.</summary>
    public static void Destroy(Icon icon)
    {
        DestroyIcon(icon.Handle);
        icon.Dispose();
    }

    private static Color ColorForPercent(int percent) => percent switch
    {
        < 60 => Color.FromArgb(46, 160, 67),
        < 85 => Color.FromArgb(210, 153, 34),
        _ => Color.FromArgb(207, 34, 46),
    };
}
