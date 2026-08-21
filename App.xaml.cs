using System.Windows;
using System.Windows.Controls;
using H.NotifyIcon;
using ClaudeUsageTracker.Models;
using ClaudeUsageTracker.Services;
using ClaudeUsageTracker.Views;

namespace ClaudeUsageTracker;

public partial class App : Application
{
    private TaskbarIcon? _trayIcon;
    private PopupWindow? _popup;
    private CredentialStore _store = null!;
    private ClaudeApiClient _client = null!;
    private UsagePoller _poller = null!;
    private System.Drawing.Icon? _currentTrayIconHandle;
    private readonly HashSet<string> _notifiedThresholds = new();

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var themeDict = new ResourceDictionary
        {
            Source = new Uri(
                WindowGlass.IsLightTheme() ? "Resources/LightTheme.xaml" : "Resources/DarkTheme.xaml",
                UriKind.Relative)
        };
        Resources.MergedDictionaries.Add(themeDict);

        _store = new CredentialStore();
        _client = new ClaudeApiClient();
        _poller = new UsagePoller(_client, _store);

        _trayIcon = (TaskbarIcon)Resources["TrayIcon"];
        _trayIcon.ToolTipText = "Claude Usage Tracker";
        _trayIcon.ContextMenu = BuildContextMenu();
        _trayIcon.TrayLeftMouseUp += (_, _) => TogglePopup();
        SetTrayIcon(TrayIconRenderer.Render(0, isError: true));
        _trayIcon.ForceCreate();

        _popup = new PopupWindow(_store);
        _popup.SettingsRequested += (_, _) => OpenSettings();

        _poller.SnapshotReceived += OnSnapshotReceived;
        _poller.SessionExpired += OnSessionExpired;
        _poller.Error += OnPollerError;

        try
        {
            await _client.InitializeAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Couldn't start the embedded browser component (WebView2) this app needs to talk to claude.ai.\n\n{ex.Message}\n\nMake sure the WebView2 Runtime is installed - it ships with Windows 11 by default, or can be installed from Microsoft's website.",
                "Claude Usage Tracker", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
            return;
        }

        var settings = _store.Load();
        var loggedIn = await _client.IsLoggedInAsync();
        if (!loggedIn || string.IsNullOrEmpty(settings.OrganizationId))
        {
            _popup.ShowUnconfigured();
            OpenSettings();
        }
        else if (settings.PinPopup)
        {
            PositionPopupNearTray();
            _popup.Show();
        }

        _poller.Start();
    }

    private ContextMenu BuildContextMenu()
    {
        var menu = new ContextMenu();

        var openItem = new MenuItem { Header = "Show usage" };
        openItem.Click += (_, _) => TogglePopup();
        menu.Items.Add(openItem);

        var refreshItem = new MenuItem { Header = "Refresh now" };
        refreshItem.Click += async (_, _) => await _poller.RefreshNowAsync();
        menu.Items.Add(refreshItem);

        var settingsItem = new MenuItem { Header = "Settings..." };
        settingsItem.Click += (_, _) => OpenSettings();
        menu.Items.Add(settingsItem);

        menu.Items.Add(new Separator());

        var exitItem = new MenuItem { Header = "Exit" };
        exitItem.Click += (_, _) => Shutdown();
        menu.Items.Add(exitItem);

        return menu;
    }

    private void TogglePopup()
    {
        if (_popup is null) return;
        _poller.NotifyActivity();

        if (_popup.IsVisible)
        {
            _popup.Hide();
            return;
        }

        PositionPopupNearTray();
        _popup.Show();
        _popup.Activate();
    }

    private void PositionPopupNearTray()
    {
        if (_popup is null) return;

        var settings = _store.Load();
        if (settings.PopupLeft is double left && settings.PopupTop is double top && IsPositionOnScreen(left, top))
        {
            _popup.Left = left;
            _popup.Top = top;
            return;
        }

        var workArea = SystemParameters.WorkArea;
        _popup.Left = workArea.Right - _popup.Width - 12;
        _popup.Top = workArea.Bottom - _popup.Height - 12;
    }

    /// <summary>
    /// Guards against a saved drag position becoming unreachable - e.g. a second monitor it was
    /// dragged onto gets disconnected later - by falling back to the default corner instead of
    /// showing the popup somewhere the user can no longer see or reach it.
    /// </summary>
    private static bool IsPositionOnScreen(double left, double top)
    {
        const double margin = 50; // a sliver on-screen still counts as reachable
        return left > SystemParameters.VirtualScreenLeft - margin
            && left < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth
            && top > SystemParameters.VirtualScreenTop - margin
            && top < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight;
    }

    private void OpenSettings()
    {
        var settingsWindow = new SettingsWindow(_store, _client);
        settingsWindow.SettingsSaved += (_, _) =>
        {
            DebugLog.Write("App: SettingsSaved fired, restarting poller and refreshing now");
            _poller.Start();
            _ = _poller.RefreshNowAsync();
        };
        settingsWindow.ShowDialog();
    }

    private void OnSnapshotReceived(object? sender, UsageSnapshot snapshot)
    {
        Dispatcher.Invoke(() =>
        {
            DebugLog.Write($"App: OnSnapshotReceived, updating tray icon to {(int)Math.Round(snapshot.FiveHour.UtilizationPct)}%");
            _popup?.ShowSnapshot(snapshot);
            SetTrayIcon(TrayIconRenderer.Render((int)Math.Round(snapshot.FiveHour.UtilizationPct)));
            if (_trayIcon is not null)
            {
                _trayIcon.ToolTipText =
                    $"Claude Usage \u2014 5h: {snapshot.FiveHour.UtilizationPct:0}% \u00b7 7d: {snapshot.SevenDay.UtilizationPct:0}% \u00b7 7d Opus: {snapshot.SevenDayOpus.UtilizationPct:0}%";
            }

            MaybeNotifyThreshold("5-hour", snapshot.FiveHour.UtilizationPct);
            MaybeNotifyThreshold("7-day", snapshot.SevenDay.UtilizationPct);
        });
    }

    private void MaybeNotifyThreshold(string label, double pct)
    {
        var settings = _store.Load();
        if (!settings.NotifyAtThresholds) return;

        foreach (var threshold in new[] { 95, 80 })
        {
            var key = $"{label}:{threshold}";
            if (pct >= threshold)
            {
                if (_notifiedThresholds.Add(key))
                    _trayIcon?.ShowNotification("Claude usage alert", $"{label} usage has reached {pct:0}%.");
                return;
            }
        }

        _notifiedThresholds.RemoveWhere(k => k.StartsWith(label + ":"));
    }

    private void OnSessionExpired(object? sender, EventArgs e)
    {
        DebugLog.Write("App: OnSessionExpired");
        Dispatcher.Invoke(() =>
        {
            _popup?.ShowSessionExpired();
            SetTrayIcon(TrayIconRenderer.Render(0, isError: true));
        });
    }

    private void OnPollerError(object? sender, Exception ex)
    {
        DebugLog.Write($"App: OnPollerError - {ex}");
        Dispatcher.Invoke(() =>
        {
            _popup?.ShowError("Couldn't reach claude.ai. Will retry automatically.");
        });
    }

    private void SetTrayIcon(System.Drawing.Icon icon)
    {
        if (_trayIcon is null) return;
        var old = _currentTrayIconHandle;
        _trayIcon.Icon = icon;
        _currentTrayIconHandle = icon;
        if (old is not null)
            TrayIconRenderer.Destroy(old);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _poller.Dispose();
        _ = _client.DisposeAsync();
        _trayIcon?.Dispose();
        base.OnExit(e);
    }
}
