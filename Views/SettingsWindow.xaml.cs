using System.Windows;
using System.Windows.Media;
using ClaudeUsageTracker.Models;
using ClaudeUsageTracker.Services;

namespace ClaudeUsageTracker.Views;

public partial class SettingsWindow : Window
{
    private readonly CredentialStore _store;
    private readonly ClaudeApiClient _client;
    private readonly AppSettings _settings;
    private List<Organization> _organizations = new();

    public event EventHandler? SettingsSaved;

    public SettingsWindow(CredentialStore store, ClaudeApiClient client)
    {
        InitializeComponent();
        _store = store;
        _client = client;
        _settings = _store.Load();

        IntervalSlider.Value = _settings.RefreshIntervalSeconds;
        IntervalValueText.Text = _settings.RefreshIntervalSeconds.ToString();
        AutoStartCheck.IsChecked = _settings.AutoStart;
        NotifyCheck.IsChecked = _settings.NotifyAtThresholds;
        ClearStyleRadio.IsChecked = _settings.ClearGlassMode;
        SolidStyleRadio.IsChecked = !_settings.ClearGlassMode;

        if (!string.IsNullOrEmpty(_settings.OrganizationId))
        {
            _organizations = new List<Organization>
            {
                new() { Id = _settings.OrganizationId, Name = _settings.OrganizationName ?? _settings.OrganizationId }
            };
            OrganizationCombo.ItemsSource = _organizations;
            OrganizationCombo.SelectedIndex = 0;
        }

        Loaded += async (_, _) => await RefreshLoginStatusAsync();
    }

    private async Task RefreshLoginStatusAsync()
    {
        LoginStatusText.Text = "Checking login status...";
        try
        {
            var loggedIn = await _client.IsLoggedInAsync();
            LoginStatusText.Text = loggedIn ? "Logged in to claude.ai" : "Not logged in";
            LoginStatusText.Foreground = loggedIn ? Brushes.SeaGreen : Brushes.Gray;
            LoginButton.Content = loggedIn ? "Log in as someone else" : "Log in with claude.ai";

            if (loggedIn && _organizations.Count == 0)
                await LoadOrganizationsAsync();
        }
        catch (Exception ex)
        {
            LoginStatusText.Text = $"Couldn't check login status: {ex.Message}";
            LoginStatusText.Foreground = Brushes.Red;
        }
    }

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        DebugLog.Write("SettingsWindow: LoginButton_Click, creating LoginWindow");
        var loginWindow = new LoginWindow { Owner = this };
        DebugLog.Write("SettingsWindow: calling ShowDialog");
        loginWindow.ShowDialog();
        DebugLog.Write("SettingsWindow: ShowDialog returned");

        await RefreshLoginStatusAsync();
    }

    private async void RefreshOrgsButton_Click(object sender, RoutedEventArgs e) => await LoadOrganizationsAsync();

    private async Task LoadOrganizationsAsync()
    {
        RefreshOrgsButton.IsEnabled = false;
        StatusMessage.Foreground = Brushes.Gray;
        StatusMessage.Text = "Loading organizations...";

        try
        {
            _organizations = (await _client.DiscoverOrganizationsAsync()).ToList();
            OrganizationCombo.ItemsSource = _organizations;

            if (_organizations.Count > 0)
            {
                var previousId = _settings.OrganizationId;
                var match = _organizations.FirstOrDefault(o => o.Id == previousId);
                OrganizationCombo.SelectedItem = match ?? _organizations[0];
                StatusMessage.Foreground = Brushes.SeaGreen;
                StatusMessage.Text = $"Found {_organizations.Count} organization(s).";
            }
            else
            {
                StatusMessage.Foreground = Brushes.OrangeRed;
                StatusMessage.Text = "No organizations were found. Make sure you're logged in.";
            }
        }
        catch (ClaudeSessionExpiredException ex)
        {
            StatusMessage.Foreground = Brushes.Red;
            StatusMessage.Text = ex.Message;
        }
        catch (Exception ex)
        {
            StatusMessage.Foreground = Brushes.Red;
            StatusMessage.Text = $"Couldn't reach claude.ai: {ex.Message}";
        }
        finally
        {
            RefreshOrgsButton.IsEnabled = true;
        }
    }

    private void IntervalSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (IntervalValueText is null) return;
        IntervalValueText.Text = ((int)e.NewValue).ToString();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (OrganizationCombo.SelectedItem is not Organization selectedOrg)
        {
            StatusMessage.Foreground = Brushes.Red;
            StatusMessage.Text = "Log in and pick an organization first.";
            return;
        }

        _settings.OrganizationId = selectedOrg.Id;
        _settings.OrganizationName = selectedOrg.Name;
        _settings.RefreshIntervalSeconds = (int)IntervalSlider.Value;
        _settings.AutoStart = AutoStartCheck.IsChecked == true;
        _settings.NotifyAtThresholds = NotifyCheck.IsChecked == true;
        _settings.ClearGlassMode = ClearStyleRadio.IsChecked == true;
        _store.Save(_settings);
        DebugLog.Write($"SettingsWindow: saved org={_settings.OrganizationId} ({_settings.OrganizationName}), interval={_settings.RefreshIntervalSeconds}s, autoStart={_settings.AutoStart}");

        AutoStartManager.SetEnabled(_settings.AutoStart);

        SettingsSaved?.Invoke(this, EventArgs.Empty);
        Close();
    }
}
