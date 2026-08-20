using System.Windows;
using ClaudeUsageTracker.Models;

namespace ClaudeUsageTracker.Views;

public partial class PopupWindow : Window
{
    public event EventHandler? SettingsRequested;

    public PopupWindow()
    {
        InitializeComponent();
    }

    public void ShowUnconfigured()
    {
        StatusText.Text = "Not logged in. Click the gear icon to log in with claude.ai.";
        FiveHourLabel.Text = "5-hour: -";
        SevenDayLabel.Text = "7-day: -";
        SevenDayOpusLabel.Text = "7-day Opus: -";
        FiveHourBar.Value = 0;
        SevenDayBar.Value = 0;
        SevenDayOpusBar.Value = 0;
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

        FiveHourBar.Value = snapshot.FiveHour.UtilizationPct;
        FiveHourLabel.Text = $"5-hour: {snapshot.FiveHour.UtilizationPct:0}%{FormatReset(snapshot.FiveHour.ResetAt)}";

        SevenDayBar.Value = snapshot.SevenDay.UtilizationPct;
        SevenDayLabel.Text = $"7-day: {snapshot.SevenDay.UtilizationPct:0}%{FormatReset(snapshot.SevenDay.ResetAt)}";

        SevenDayOpusBar.Value = snapshot.SevenDayOpus.UtilizationPct;
        SevenDayOpusLabel.Text = $"7-day Opus: {snapshot.SevenDayOpus.UtilizationPct:0}%{FormatReset(snapshot.SevenDayOpus.ResetAt)}";

        LastRefreshedText.Text = $"Last updated {snapshot.LastRefreshed:t}";
    }

    private static string FormatReset(DateTimeOffset? resetAt)
    {
        if (resetAt is null) return "";
        var remaining = resetAt.Value - DateTimeOffset.Now;
        if (remaining <= TimeSpan.Zero) return " (resetting...)";
        return remaining.TotalHours >= 1
            ? $" (resets in {(int)remaining.TotalHours}h {remaining.Minutes}m)"
            : $" (resets in {remaining.Minutes}m)";
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e) => SettingsRequested?.Invoke(this, EventArgs.Empty);

    private void Window_Deactivated(object? sender, EventArgs e) => Hide();

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }
}
