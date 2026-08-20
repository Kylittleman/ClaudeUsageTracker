using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ClaudeUsageTracker.Services;

public sealed class AppSettings
{
    public string? OrganizationId { get; set; }
    public string? OrganizationName { get; set; }
    public int RefreshIntervalSeconds { get; set; } = 60;
    public bool AutoStart { get; set; } = true;
    public bool NotifyAtThresholds { get; set; } = true;
    public string? EncryptedSessionKey { get; set; }
}

/// <summary>
/// Persists non-secret settings as plain JSON and the claude.ai session key
/// encrypted with Windows DPAPI (CurrentUser scope) - the same trust boundary
/// the macOS originals get from Keychain: readable only by this Windows user
/// on this machine.
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

    public string? GetSessionKey(AppSettings settings)
    {
        if (string.IsNullOrEmpty(settings.EncryptedSessionKey))
            return null;

        try
        {
            var encryptedBytes = Convert.FromBase64String(settings.EncryptedSessionKey);
            var decryptedBytes = ProtectedData.Unprotect(encryptedBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decryptedBytes);
        }
        catch (CryptographicException)
        {
            return null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    public void SetSessionKey(AppSettings settings, string sessionKey)
    {
        var bytes = Encoding.UTF8.GetBytes(sessionKey);
        var encryptedBytes = ProtectedData.Protect(bytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
        settings.EncryptedSessionKey = Convert.ToBase64String(encryptedBytes);
    }

    public void ClearSessionKey(AppSettings settings)
    {
        settings.EncryptedSessionKey = null;
    }
}
