using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using ClaudeUsageTracker.Models;
using ClaudeUsageTracker.Services;

namespace ClaudeUsageTracker.Views;

public partial class PopupWindow : Window
{
    private const double TrackWidth = 260;

    private readonly CredentialStore _store;
    private bool _isPinned;

    public event EventHandler? SettingsRequested;

    public PopupWindow(CredentialStore store)
    {
        InitializeComponent();
        _store = store;

        _isPinned = _store.Load().PinPopup;
        PinButton.IsChecked = _isPinned;
    }

    public bool IsPinned => _isPinned;

    public void ShowUnconfigured()
    {
        SetStatus("Not logged in. Click the gear icon to log in with claude.ai.");
        FiveHourPctText.Text = "-";
        SevenDayPctText.Text = "-";
        AnimateBar(FiveHourFill, 0);
        AnimateBar(SevenDayFill, 0);
        FiveHourResetText.Text = "";
        SevenDayResetText.Text = "";
    }

    public void ShowSessionExpired() => SetStatus("Session expired. Click the gear icon to log in again.");

    public void ShowError(string message) => SetStatus(message);

    public void ShowSnapshot(UsageSnapshot snapshot)
    {
        SetStatus("");

        FiveHourPctText.Text = $"{snapshot.FiveHour.UtilizationPct:0}%";
        AnimateBar(FiveHourFill, snapshot.FiveHour.UtilizationPct);
        FiveHourResetText.Text = FormatReset(snapshot.FiveHour.ResetAt);

        SevenDayPctText.Text = $"{snapshot.SevenDay.UtilizationPct:0}%";
        AnimateBar(SevenDayFill, snapshot.SevenDay.UtilizationPct);
        SevenDayResetText.Text = FormatReset(snapshot.SevenDay.ResetAt);
    }

    private void SetStatus(string message)
    {
        StatusText.Text = message;
        // Collapse entirely rather than leaving an empty line's worth of reserved space -
        // otherwise the bottom of the card carries visibly more whitespace than the top.
        StatusText.Visibility = string.IsNullOrEmpty(message) ? Visibility.Collapsed : Visibility.Visible;
    }

    private static void AnimateBar(FrameworkElement fill, double pct)
    {
        var targetWidth = Math.Clamp(pct / 100.0, 0, 1) * TrackWidth;
        var animation = new DoubleAnimation
        {
            To = targetWidth,
            Duration = TimeSpan.FromMilliseconds(550),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        fill.BeginAnimation(WidthProperty, animation);
    }

    private static string FormatReset(DateTimeOffset? resetAt)
    {
        if (resetAt is null) return "";
        var remaining = resetAt.Value - DateTimeOffset.Now;
        if (remaining <= TimeSpan.Zero) return "Resetting...";
        return remaining.TotalHours >= 1
            ? $"Resets in {(int)remaining.TotalHours}h {remaining.Minutes}m"
            : $"Resets in {remaining.Minutes}m";
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e) => SettingsRequested?.Invoke(this, EventArgs.Empty);

    private void PinButton_Click(object sender, RoutedEventArgs e)
    {
        _isPinned = PinButton.IsChecked == true;

        var settings = _store.Load();
        settings.PinPopup = _isPinned;
        _store.Save(settings);
    }

    private void Window_Deactivated(object? sender, EventArgs e)
    {
        if (!_isPinned) Hide();
    }

    private void HeaderPanel_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton != System.Windows.Input.MouseButton.Left) return;

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            return; // mouse was released before the drag operation could start
        }

        var settings = _store.Load();
        settings.PopupLeft = Left;
        settings.PopupTop = Top;
        _store.Save(settings);
        DebugLog.Write($"PopupWindow: dragged to Left={Left}, Top={Top}");
    }

    /// <summary>
    /// Color (White/Black) and Style (Solid/Clear) are independent choices, not a single
    /// light/dark-follows-Windows-theme toggle - a truly see-through Clear card can land over
    /// any arbitrary desktop content, so at low opacity even a "Black" tint can visually read as
    /// washed-out/light if what's behind it is bright. Making both axes explicit and
    /// user-controlled avoids that ambiguity entirely. The border and all text use the same
    /// "ink" color as a simple, consistent rule; Clear mode additionally gets a drop shadow in
    /// the opposite tone for legibility over unknown backgrounds, and a top highlight gradient
    /// suggesting light catching a curved glass surface.
    /// </summary>
    private void ApplyAppearance()
    {
        var settings = _store.Load();
        var isLight = settings.PopupLightColor;
        var isClear = settings.ClearGlassMode;

        var ink = isLight ? Colors.Black : Colors.White;
        var inkInverse = isLight ? Colors.White : Colors.Black;

        Resources["PrimaryTextBrush"] = new SolidColorBrush(ink);
        Resources["SecondaryTextBrush"] = new SolidColorBrush(Color.FromArgb(isClear ? (byte)204 : (byte)179, ink.R, ink.G, ink.B));
        Resources["TertiaryTextBrush"] = new SolidColorBrush(Color.FromArgb(isClear ? (byte)195 : (byte)115, ink.R, ink.G, ink.B));
        Resources["TrackBrush"] = new SolidColorBrush(Color.FromArgb(isClear ? (byte)46 : (byte)32, ink.R, ink.G, ink.B));

        if (isClear)
        {
            GlassCard.Background = new SolidColorBrush(Color.FromArgb(34, inkInverse.R, inkInverse.G, inkInverse.B));
            GlassCard.BorderBrush = new SolidColorBrush(Color.FromArgb(90, ink.R, ink.G, ink.B));
            HighlightOverlay.Visibility = Visibility.Visible;
            // Dark text over a translucent white tint needs a much stronger halo than light text
            // over a dark tint does - black has less inherent "pop" against an unpredictable
            // desktop behind it, so White+Clear gets a tighter, fully-opaque, non-directional
            // glow (ShadowDepth 0 = symmetric outline) rather than the softer offset shadow that
            // already reads fine for white text.
            ContentGrid.Effect = isLight
                ? new DropShadowEffect { Color = inkInverse, BlurRadius = 4, ShadowDepth = 0, Opacity = 1.0 }
                : new DropShadowEffect { Color = inkInverse, BlurRadius = 6, ShadowDepth = 1, Direction = 270, Opacity = 0.6 };
        }
        else
        {
            GlassCard.Background = new SolidColorBrush(isLight
                ? Color.FromArgb(192, 255, 255, 255)
                : Color.FromArgb(176, 20, 20, 25));
            GlassCard.BorderBrush = new SolidColorBrush(Color.FromArgb(isLight ? (byte)26 : (byte)31, ink.R, ink.G, ink.B));
            HighlightOverlay.Visibility = Visibility.Collapsed;
            ContentGrid.Effect = null;
        }
    }

    private void Window_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is not true) return;

        ApplyAppearance();

        Opacity = 0;
        CardSlide.Y = 14;

        var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
        };
        var slide = new DoubleAnimation(14, 0, TimeSpan.FromMilliseconds(280))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        BeginAnimation(OpacityProperty, fade);
        CardSlide.BeginAnimation(TranslateTransform.YProperty, slide);
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }
}
