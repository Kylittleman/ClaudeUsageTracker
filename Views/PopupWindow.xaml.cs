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
        StatusText.Text = "Not logged in. Click the gear icon to log in with claude.ai.";
        FiveHourPctText.Text = "-";
        SevenDayPctText.Text = "-";
        AnimateBar(FiveHourFill, 0);
        AnimateBar(SevenDayFill, 0);
        FiveHourResetText.Text = "";
        SevenDayResetText.Text = "";
        LastRefreshedText.Text = "";
    }

    public void ShowSessionExpired()
    {
        StatusText.Text = "Session expired. Click the gear icon to log in again.";
    }

    public void ShowError(string message)
    {
        StatusText.Text = message;
    }

    public void ShowSnapshot(UsageSnapshot snapshot)
    {
        StatusText.Text = "";

        FiveHourPctText.Text = $"{snapshot.FiveHour.UtilizationPct:0}%";
        AnimateBar(FiveHourFill, snapshot.FiveHour.UtilizationPct);
        FiveHourResetText.Text = FormatReset(snapshot.FiveHour.ResetAt);

        SevenDayPctText.Text = $"{snapshot.SevenDay.UtilizationPct:0}%";
        AnimateBar(SevenDayFill, snapshot.SevenDay.UtilizationPct);
        SevenDayResetText.Text = FormatReset(snapshot.SevenDay.ResetAt);

        LastRefreshedText.Text = $"Last updated {snapshot.LastRefreshed:t}";
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

    /// <summary>
    /// "Clear" is a deliberately different visual language from "Solid", not just a lower-alpha
    /// version of it: a truly see-through surface can land over any arbitrary desktop content,
    /// so it can't rely on the comfortable light/dark theme-matched text contrast Solid mode
    /// gets from its near-opaque tint. Clear mode instead uses a fixed white-text-with-shadow
    /// treatment (the same trick game overlays and photo-widget UIs use for legibility over
    /// unknown backgrounds), a brighter rim to define the otherwise-invisible edge, and a top
    /// highlight gradient suggesting light catching a curved glass surface.
    /// </summary>
    private void ApplyAppearance()
    {
        var clear = _store.Load().ClearGlassMode;

        if (clear)
        {
            Resources["PrimaryTextBrush"] = Brushes.White;
            Resources["SecondaryTextBrush"] = new SolidColorBrush(Color.FromArgb(204, 255, 255, 255));
            Resources["TertiaryTextBrush"] = new SolidColorBrush(Color.FromArgb(140, 255, 255, 255));
            Resources["TrackBrush"] = new SolidColorBrush(Color.FromArgb(46, 255, 255, 255));

            GlassCard.Background = new SolidColorBrush(Color.FromArgb(34, 0, 0, 0));
            GlassCard.BorderBrush = new SolidColorBrush(Color.FromArgb(90, 255, 255, 255));
            HighlightOverlay.Visibility = Visibility.Visible;
            ContentGrid.Effect = new DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 6,
                ShadowDepth = 1,
                Direction = 270,
                Opacity = 0.6,
            };
        }
        else
        {
            Resources.Remove("PrimaryTextBrush");
            Resources.Remove("SecondaryTextBrush");
            Resources.Remove("TertiaryTextBrush");
            Resources.Remove("TrackBrush");

            GlassCard.SetResourceReference(System.Windows.Controls.Border.BackgroundProperty, "GlassOverlayBrush");
            GlassCard.SetResourceReference(System.Windows.Controls.Border.BorderBrushProperty, "BorderBrush2");
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
