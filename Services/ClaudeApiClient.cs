using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using ClaudeUsageTracker.Models;

namespace ClaudeUsageTracker.Services;

public sealed class ClaudeSessionExpiredException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public string Detail { get; }

    public ClaudeSessionExpiredException(HttpStatusCode statusCode, string detail)
        : base($"The claude.ai session key was rejected ({(int)statusCode} {statusCode}).")
    {
        StatusCode = statusCode;
        Detail = detail;
    }
}

/// <summary>
/// Talks directly to claude.ai's own web API using a copied `sessionKey` cookie,
/// the same mechanism Usage4Claude and Claude-Usage-Tracker use on macOS.
/// </summary>
public sealed class ClaudeApiClient : IDisposable
{
    private const string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36";

    private readonly HttpClient _http;

    public ClaudeApiClient()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
        _http.DefaultRequestHeaders.TryAddWithoutValidation("Origin", "https://claude.ai");
        _http.DefaultRequestHeaders.TryAddWithoutValidation("Referer", "https://claude.ai/");
        _http.DefaultRequestHeaders.TryAddWithoutValidation("sec-ch-ua", "\"Chromium\";v=\"126\", \"Not.A/Brand\";v=\"24\", \"Google Chrome\";v=\"126\"");
        _http.DefaultRequestHeaders.TryAddWithoutValidation("sec-ch-ua-mobile", "?0");
        _http.DefaultRequestHeaders.TryAddWithoutValidation("sec-ch-ua-platform", "\"Windows\"");
        _http.DefaultRequestHeaders.TryAddWithoutValidation("sec-fetch-site", "same-origin");
        _http.DefaultRequestHeaders.TryAddWithoutValidation("sec-fetch-mode", "cors");
        _http.DefaultRequestHeaders.TryAddWithoutValidation("sec-fetch-dest", "empty");
    }

    public async Task<IReadOnlyList<Organization>> DiscoverOrganizationsAsync(string sessionKey, CancellationToken ct = default)
    {
        using var response = await SendAsync(HttpMethod.Get, "https://claude.ai/api/organizations", sessionKey, ct);
        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);

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

    public async Task<UsageSnapshot> GetUsageAsync(string sessionKey, string organizationId, CancellationToken ct = default)
    {
        using var response = await SendAsync(HttpMethod.Get, $"https://claude.ai/api/organizations/{organizationId}/usage", sessionKey, ct);
        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        return new UsageSnapshot
        {
            FiveHour = ParseWindow(root, "five_hour"),
            SevenDay = ParseWindow(root, "seven_day"),
            SevenDayOpus = ParseWindow(root, "seven_day_opus"),
            LastRefreshed = DateTimeOffset.Now,
        };
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, string sessionKey, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.TryAddWithoutValidation("Cookie", $"sessionKey={sessionKey}");

        var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            var snippet = body.Length > 300 ? body[..300] : body;

            if (LooksLikeBotChallenge(body))
                snippet = "(claude.ai returned a bot-check/challenge page instead of JSON - this is not a rejected key, it's the request being blocked before it reached the API.) " + snippet;

            response.Dispose();
            throw new ClaudeSessionExpiredException(response.StatusCode, snippet);
        }
        return response;
    }

    private static bool LooksLikeBotChallenge(string body)
    {
        var lower = body.ToLowerInvariant();
        return lower.Contains("cf-browser-verification")
            || lower.Contains("just a moment")
            || lower.Contains("attention required")
            || lower.Contains("<html");
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

    public void Dispose() => _http.Dispose();
}
