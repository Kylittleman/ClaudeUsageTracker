using System.Text.Json;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using ClaudeUsageTracker.Models;

namespace ClaudeUsageTracker.Services;

public sealed class ClaudeSessionExpiredException : Exception
{
    public ClaudeSessionExpiredException(string detail)
        : base($"claude.ai session is missing or expired. {detail}") { }
}

/// <summary>
/// Fetches claude.ai usage data through a hidden, real Edge/Chromium engine (WebView2) instead
/// of a raw HttpClient. claude.ai's usage endpoints sit behind Cloudflare bot protection that
/// blocks non-browser HTTP requests outright - even ones carrying a valid, correctly copied
/// session cookie - but passes automatically for a genuine browser engine like this one.
/// </summary>
public sealed class ClaudeApiClient : IAsyncDisposable
{
    private Window? _hostWindow;
    private WebView2? _webView;
    private readonly SemaphoreSlim _navLock = new(1, 1);

    public async Task InitializeAsync()
    {
        if (_webView is not null) return;

        var environment = await BrowserEnvironment.GetAsync();

        _hostWindow = new Window
        {
            Width = 50,
            Height = 50,
            Left = -32000,
            Top = -32000,
            WindowStyle = WindowStyle.None,
            ShowInTaskbar = false,
            ShowActivated = false,
        };

        _webView = new WebView2();
        _hostWindow.Content = _webView;
        _hostWindow.Show();

        await _webView.EnsureCoreWebView2Async(environment);
    }

    public async Task<bool> IsLoggedInAsync()
    {
        await InitializeAsync();
        var cookies = await _webView!.CoreWebView2.CookieManager.GetCookiesAsync("https://claude.ai");
        return cookies.Any(c => c.Name == "sessionKey" && !string.IsNullOrEmpty(c.Value));
    }

    public async Task<IReadOnlyList<Organization>> DiscoverOrganizationsAsync(CancellationToken ct = default)
    {
        var text = await FetchJsonTextAsync("https://claude.ai/api/organizations", ct);
        using var doc = JsonDocument.Parse(text);

        var results = new List<Organization>();
        if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in doc.RootElement.EnumerateArray())
            {
                var id = GetString(element, "uuid") ?? GetString(element, "id");
                var name = GetString(element, "name") ?? "Unknown organization";
                if (id is not null)
                    results.Add(new Organization { Id = id, Name = name });
            }
        }
        return results;
    }

    public async Task<UsageSnapshot> GetUsageAsync(string organizationId, CancellationToken ct = default)
    {
        var text = await FetchJsonTextAsync($"https://claude.ai/api/organizations/{organizationId}/usage", ct);
        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;

        return new UsageSnapshot
        {
            FiveHour = ParseWindow(root, "five_hour"),
            SevenDay = ParseWindow(root, "seven_day"),
            SevenDayOpus = ParseWindow(root, "seven_day_opus"),
            LastRefreshed = DateTimeOffset.Now,
        };
    }

    private async Task<string> FetchJsonTextAsync(string url, CancellationToken ct)
    {
        DebugLog.Write($"ClaudeApiClient: fetching {url}");
        await InitializeAsync();
        await _navLock.WaitAsync(ct);
        try
        {
            await NavigateAsync(url, ct);
            DebugLog.Write($"ClaudeApiClient: navigation completed for {url}");

            for (var attempt = 0; attempt < 6; attempt++)
            {
                var text = await GetBodyTextAsync();
                var preview = text.Length > 120 ? text[..120] : text;
                DebugLog.Write($"ClaudeApiClient: attempt {attempt}, looksLikeJson={LooksLikeJson(text)}, preview=[{preview}]");
                if (LooksLikeJson(text))
                    return text;

                // A Cloudflare interstitial resolves itself in a real browser within a few
                // seconds; give it a moment and check again before giving up.
                await Task.Delay(1000, ct);
            }

            DebugLog.Write($"ClaudeApiClient: gave up on {url} after 6 attempts, no JSON");
            throw new ClaudeSessionExpiredException(
                "claude.ai kept returning a non-JSON (bot-check or login) page instead of usage data. Try logging in again.");
        }
        finally
        {
            _navLock.Release();
        }
    }

    private Task NavigateAsync(string url, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Handler(object? s, CoreWebView2NavigationCompletedEventArgs e) => tcs.TrySetResult();
        _webView!.CoreWebView2.NavigationCompleted += Handler;
        using var reg = ct.Register(() => tcs.TrySetCanceled(ct));

        try
        {
            _webView.CoreWebView2.Navigate(url);
        }
        catch
        {
            _webView.CoreWebView2.NavigationCompleted -= Handler;
            throw;
        }

        return WaitAndUnhook();

        async Task WaitAndUnhook()
        {
            try { await tcs.Task; }
            finally { _webView!.CoreWebView2.NavigationCompleted -= Handler; }
        }
    }

    private async Task<string> GetBodyTextAsync()
    {
        var raw = await _webView!.CoreWebView2.ExecuteScriptAsync("document.body ? document.body.innerText : ''");
        return JsonSerializer.Deserialize<string>(raw) ?? "";
    }

    private static bool LooksLikeJson(string text)
    {
        var trimmed = text.TrimStart();
        return trimmed.StartsWith('{') || trimmed.StartsWith('[');
    }

    private static UsageWindow ParseWindow(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var el) || el.ValueKind != JsonValueKind.Object)
            return new UsageWindow { UtilizationPct = 0, ResetAt = null };

        double pct = 0;
        if (el.TryGetProperty("utilization_pct", out var pctEl) && pctEl.ValueKind is JsonValueKind.Number)
            pct = pctEl.GetDouble();
        else if (el.TryGetProperty("utilization", out var pctEl2) && pctEl2.ValueKind is JsonValueKind.Number)
            pct = pctEl2.GetDouble();

        DateTimeOffset? resetAt = null;
        var resetString = GetString(el, "resets_at") ?? GetString(el, "reset_at");
        if (resetString is not null && DateTimeOffset.TryParse(resetString, out var parsed))
            resetAt = parsed;

        return new UsageWindow { UtilizationPct = pct, ResetAt = resetAt };
    }

    private static string? GetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    public ValueTask DisposeAsync()
    {
        _webView?.Dispose();
        _hostWindow?.Close();
        return ValueTask.CompletedTask;
    }
}
