using System.IO;

namespace ClaudeUsageTracker.Services;

/// <summary>Minimal debug log to a fixed file, for diagnosing tray-app issues that are hard to observe interactively.</summary>
public static class DebugLog
{
    private static readonly string LogPath = Path.Combine(Path.GetTempPath(), "ClaudeUsageTrackerDebug.log");
    private static readonly object Lock = new();

    public static void Write(string message)
    {
        try
        {
            lock (Lock)
            {
                File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss.fff} {message}\r\n");
            }
        }
        catch
        {
            // best-effort only
        }
    }
}
