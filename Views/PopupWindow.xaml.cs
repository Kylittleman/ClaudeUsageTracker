using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
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

        SourceInitialized += (_, _) =>
        {
            WindowGlass.ApplyRoundedCorners(this, Width, Height, cornerRadius: 18);
        };
    }

    public bool IsPinned => _isPinned;

    public void ShowUnconfigured()
    {
        StatusText.Text = "Not logged in. Click the gear icon to log in with claude.ai.";
        FiveHourPctText.Text = "-";
        SevenDayPctText.Text = "-";
        SevenDayOpusPctText.Text = "-";
        AnimateBar(FiveHourFill, 0);
        AnimateBar(SevenDayFill, 0);
        AnimateBar(SevenDayOpusFill, 0);
        FiveHourResetText.Text = "";
        SevenDayResetText.Text = "";
        SevenDayOpusResetText.Text = "";
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

        SevenDayOpusPctText.Text = $"{snapshot.SevenDayOpus.UtilizationPct:0}%";
        AnimateBar(SevenDayOpusFill, snapshot.SevenDayOpus.UtilizationPct);
        SevenDayOpusResetText.Text = FormatReset(snapshot.SevenDayOpus.ResetAt);

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

    private void Window_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is not true) return;

        // Recompute the rounded-corner clip on every show, not just once at window creation -
        // the window may not have been on its final monitor (with its final DPI) yet back at
        // SourceInitialized time.
        WindowGlass.ApplyRoundedCorners(this, Width, Height, cornerRadius: 18);

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
