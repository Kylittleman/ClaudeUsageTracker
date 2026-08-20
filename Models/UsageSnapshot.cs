namespace ClaudeUsageTracker.Models;

public sealed class UsageWindow
{
    public double UtilizationPct { get; init; }
    public DateTimeOffset? ResetAt { get; init; }
}

public sealed class UsageSnapshot
{
    public required UsageWindow FiveHour { get; init; }
    public required UsageWindow SevenDay { get; init; }
    public required UsageWindow SevenDayOpus { get; init; }
    public DateTimeOffset LastRefreshed { get; init; } = DateTimeOffset.Now;
}

public enum UsageStatus
{
    Unconfigured,
    Loading,
    Ok,
    SessionExpired,
    Error,
}
