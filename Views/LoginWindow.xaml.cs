using System.Windows;
using ClaudeUsageTracker.Services;

namespace ClaudeUsageTracker.Views;

public partial class LoginWindow : Window
{
    private readonly System.Windows.Threading.DispatcherTimer _pollTimer;
    private bool _loggedIn;
    private int _tickCount;

    public bool LoggedIn => _loggedIn;

    public LoginWindow()
    {
        DebugLog.Write("LoginWindow: constructor start");
        InitializeComponent();
        DebugLog.Write("LoginWindow: InitializeComponent done");

        _pollTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1),
        };
        _pollTimer.Tick += async (_, _) => await PollForLoginAsync();

        Loaded += async (_, _) => await InitializeBrowserAsync();
        Closed += (_, _) => { DebugLog.Write($"LoginWindow: Closed event fired, LoggedIn={_loggedIn}"); _pollTimer.Stop(); };
        Closing += (_, e) => DebugLog.Write($"LoginWindow: Closing event fired, Cancel={e.Cancel}");
    }

    private async Task InitializeBrowserAsync()
    {
        DebugLog.Write("LoginWindow: InitializeBrowserAsync start");
        try
        {
            var environment = await BrowserEnvironment.GetAsync();
            DebugLog.Write("LoginWindow: got shared environment");
            await Browser.EnsureCoreWebView2Async(environment);
            DebugLog.Write("LoginWindow: EnsureCoreWebView2Async done, CoreWebView2 null? " + (Browser.CoreWebView2 is null));
            Browser.CoreWebView2!.Navigate("https://claude.ai/login");
            DebugLog.Write("LoginWindow: Navigate called");
            _pollTimer.Start();
            DebugLog.Write("LoginWindow: poll timer started");
        }
        catch (Exception ex)
        {
            DebugLog.Write($"LoginWindow: EXCEPTION in InitializeBrowserAsync: {ex}");
            MessageBox.Show(
                $"Couldn't open the embedded browser for login.\n\n{ex.Message}",
                "Claude Usage Tracker", MessageBoxButton.OK, MessageBoxImage.Error);
            Close();
        }
    }

    private async Task PollForLoginAsync()
    {
        _tickCount++;
        if (Browser.CoreWebView2 is null)
        {
            DebugLog.Write($"LoginWindow: poll tick {_tickCount}, CoreWebView2 is null, returning");
            return;
        }

        try
        {
            var cookies = await Browser.CoreWebView2.CookieManager.GetCookiesAsync("https://claude.ai");
            var names = string.Join(",", cookies.Select(c => c.Name));
            DebugLog.Write($"LoginWindow: poll tick {_tickCount}, cookie count={cookies.Count}, names=[{names}], currentUri={Browser.Source}");

            if (cookies.Any(c => c.Name == "sessionKey" && !string.IsNullOrEmpty(c.Value)))
            {
                DebugLog.Write("LoginWindow: sessionKey cookie found, closing");
                _loggedIn = true;
                _pollTimer.Stop();
                Close();
            }
        }
        catch (Exception ex)
        {
            DebugLog.Write($"LoginWindow: poll tick {_tickCount} EXCEPTION: {ex.Message}");
        }
    }
}
