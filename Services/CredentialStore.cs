using System.IO;
using System.Text.Json;

namespace ClaudeUsageTracker.Services;

public sealed class AppSettings
{
    public string? OrganizationId { get; set; }
    public string? OrganizationName { get; set; }
    public int RefreshIntervalSeconds { get; set; } = 60;
    public bool AutoStart { get; set; } = true;
    public bool NotifyAtThresholds { get; set; } = true;
    public bool PinPopup { get; set; } = false;
    public bool ClearGlassMode { get; set; } = false;
    public double? PopupLeft { get; set; }
    public double? PopupTop { get; set; }
}

/// <summary>
/// Persists non-secret settings as plain JSON. Login state itself lives in the WebView2
/// profile's own cookie store (see BrowserEnvironment) - there is no separate secret to
/// encrypt or manage here.
/// </summary>
public sealed class CredentialStore
{
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClaudeUsageTracker");
    private static readonly string ConfigPath = Path.Combine(ConfigDir, "config.json");

    public AppSettings Load()
    {
        if (!File.Exists(ConfigPath))
            return new AppSettings();

        try
        {
            var json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(ConfigDir);
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigPath, json);
    }
}
