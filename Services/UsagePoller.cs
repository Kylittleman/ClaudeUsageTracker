using ClaudeUsageTracker.Models;

namespace ClaudeUsageTracker.Services;

/// <summary>
/// Adaptive-interval poller: refreshes faster while the user has recently interacted
/// with the tray/popup, and backs off to a slow interval when idle - mirroring the
/// "smart frequency" behavior described by the macOS originals.
/// </summary>
public sealed class UsagePoller : IDisposable
{
    private static readonly TimeSpan ActiveWindow = TimeSpan.FromMinutes(3);

    private readonly ClaudeApiClient _client;
    private readonly CredentialStore _store;
    private CancellationTokenSource? _cts;
    private DateTimeOffset _lastActivity = DateTimeOffset.MinValue;

    public event EventHandler<UsageSnapshot>? SnapshotReceived;
    public event EventHandler? SessionExpired;
    public event EventHandler<Exception>? Error;

    public UsagePoller(ClaudeApiClient client, CredentialStore store)
    {
        _client = client;
        _store = store;
    }

    public void NotifyActivity() => _lastActivity = DateTimeOffset.Now;

    public void Start()
    {
        Stop();
        _cts = new CancellationTokenSource();
        _ = RunLoopAsync(_cts.Token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    public Task RefreshNowAsync() => PollOnceAsync(CancellationToken.None);

    private async Task RunLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await PollOnceAsync(ct);

            var settings = _store.Load();
            var baseInterval = TimeSpan.FromSeconds(Math.Max(30, settings.RefreshIntervalSeconds));
            var isActive = (DateTimeOffset.Now - _lastActivity) < ActiveWindow;
            var interval = isActive ? baseInterval : TimeSpan.FromSeconds(baseInterval.TotalSeconds * 3);

            try
            {
                await Task.Delay(interval, ct);
            }
            catch (TaskCanceledException)
            {
                return;
            }
        }
    }

    private async Task PollOnceAsync(CancellationToken ct)
    {
        var settings = _store.Load();
        if (string.IsNullOrEmpty(settings.OrganizationId))
        {
            DebugLog.Write("UsagePoller: no OrganizationId configured, skipping poll");
            return;
        }

        try
        {
            DebugLog.Write($"UsagePoller: polling org {settings.OrganizationId}");
            var snapshot = await _client.GetUsageAsync(settings.OrganizationId, ct);
            DebugLog.Write($"UsagePoller: got snapshot 5h={snapshot.FiveHour.UtilizationPct} 7d={snapshot.SevenDay.UtilizationPct} 7dOpus={snapshot.SevenDayOpus.UtilizationPct}");
            SnapshotReceived?.Invoke(this, snapshot);
        }
        catch (ClaudeSessionExpiredException ex)
        {
            DebugLog.Write($"UsagePoller: SessionExpired - {ex.Message}");
            SessionExpired?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            DebugLog.Write($"UsagePoller: ERROR - {ex}");
            Error?.Invoke(this, ex);
        }
    }

    public void Dispose() => Stop();
}
