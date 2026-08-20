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

        var existingKey = _store.GetSessionKey(_settings);
        if (!string.IsNullOrEmpty(existingKey))
            SessionKeyBox.Password = existingKey;

        if (!string.IsNullOrEmpty(_settings.OrganizationId))
        {
            _organizations = new List<Organization>
            {
                new() { Id = _settings.OrganizationId, Name = _settings.OrganizationName ?? _settings.OrganizationId }
            };
            OrganizationCombo.ItemsSource = _organizations;
            OrganizationCombo.SelectedIndex = 0;
        }
    }

    private void IntervalSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (IntervalValueText is null) return;
        IntervalValueText.Text = ((int)e.NewValue).ToString();
    }

    private async void TestConnectionButton_Click(object sender, RoutedEventArgs e)
    {
        var sessionKey = SessionKeyBox.Password.Trim();
        if (string.IsNullOrEmpty(sessionKey))
        {
            StatusMessage.Foreground = Brushes.Red;
            StatusMessage.Text = "Paste a session key first.";
            return;
        }

        TestConnectionButton.IsEnabled = false;
        StatusMessage.Foreground = Brushes.Gray;
        StatusMessage.Text = "Checking...";

        try
        {
            _organizations = (await _client.DiscoverOrganizationsAsync(sessionKey)).ToList();
            OrganizationCombo.ItemsSource = _organizations;
            if (_organizations.Count > 0)
            {
                var previousId = _settings.OrganizationId;
                var match = _organizations.FirstOrDefault(o => o.Id == previousId);
                OrganizationCombo.SelectedItem = match ?? _organizations[0];
                StatusMessage.Foreground = Brushes.LimeGreen;
                StatusMessage.Text = $"Found {_organizations.Count} organization(s). Click Save.";
            }
            else
            {
                StatusMessage.Foreground = Brushes.OrangeRed;
                StatusMessage.Text = "Session key worked but no organizations were found.";
            }
        }
        catch (ClaudeSessionExpiredException)
        {
            StatusMessage.Foreground = Brushes.Red;
            StatusMessage.Text = "That session key was rejected (expired or invalid).";
        }
        catch (Exception ex)
        {
            StatusMessage.Foreground = Brushes.Red;
            StatusMessage.Text = $"Couldn't reach claude.ai: {ex.Message}";
        }
        finally
        {
            TestConnectionButton.IsEnabled = true;
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var sessionKey = SessionKeyBox.Password.Trim();
        if (string.IsNullOrEmpty(sessionKey))
        {
            StatusMessage.Foreground = Brushes.Red;
            StatusMessage.Text = "A session key is required.";
            return;
        }

        if (OrganizationCombo.SelectedItem is not Organization selectedOrg)
        {
            StatusMessage.Foreground = Brushes.Red;
            StatusMessage.Text = "Click \"Test & Load Organizations\" and pick an organization first.";
            return;
        }

        _store.SetSessionKey(_settings, sessionKey);
        _settings.OrganizationId = selectedOrg.Id;
        _settings.OrganizationName = selectedOrg.Name;
        _settings.RefreshIntervalSeconds = (int)IntervalSlider.Value;
        _settings.AutoStart = AutoStartCheck.IsChecked == true;
        _settings.NotifyAtThresholds = NotifyCheck.IsChecked == true;
        _store.Save(_settings);

        AutoStartManager.SetEnabled(_settings.AutoStart);

        SettingsSaved?.Invoke(this, EventArgs.Empty);
        Close();
    }
}
