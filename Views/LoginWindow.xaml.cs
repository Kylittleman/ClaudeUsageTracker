using System.Windows;
using ClaudeUsageTracker.Services;

namespace ClaudeUsageTracker.Views;

public partial class LoginWindow : Window
{
    private readonly System.Windows.Threading.DispatcherTimer _pollTimer;
    private bool _loggedIn;

    public bool LoggedIn => _loggedIn;

    public LoginWindow()
    {
        InitializeComponent();

        _pollTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1),
        };
        _pollTimer.Tick += async (_, _) => await PollForLoginAsync();

        Loaded += async (_, _) => await InitializeBrowserAsync();
        Closed += (_, _) => _pollTimer.Stop();
    }

    private async Task InitializeBrowserAsync()
    {
        var environment = await BrowserEnvironment.GetAsync();
        await Browser.EnsureCoreWebView2Async(environment);
        _pollTimer.Start();
    }

    private async Task PollForLoginAsync()
    {
        if (Browser.CoreWebView2 is null) return;

        try
        {
            var cookies = await Browser.CoreWebView2.CookieManager.GetCookiesAsync("https://claude.ai");
            if (cookies.Any(c => c.Name == "sessionKey" && !string.IsNullOrEmpty(c.Value)))
            {
                _loggedIn = true;
                _pollTimer.Stop();
                Close();
            }
        }
        catch (Exception)
        {
            // Transient failures while the page is navigating are expected; just try again next tick.
        }
    }
}
