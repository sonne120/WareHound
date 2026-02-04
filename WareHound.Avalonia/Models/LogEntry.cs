using Avalonia.Media;

namespace WareHound.Avalonia.Models;

public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error
}

public class LogEntry
{
    public DateTime Timestamp { get; set; }
    public LogLevel Level { get; set; }
    public string Message { get; set; } = "";
    public string? Exception { get; set; }

    public string LevelDisplay => Level switch
    {
        LogLevel.Debug => "DEBUG",
        LogLevel.Info => "INFO",
        LogLevel.Warning => "WARN",
        LogLevel.Error => "ERROR",
        _ => "UNKNOWN"
    };

    public string TimestampDisplay => Timestamp.ToString("HH:mm:ss.fff");

    public Color LevelColor => Level switch
    {
        LogLevel.Debug => Color.Parse("#8E8E93"),    // Gray
        LogLevel.Info => Color.Parse("#007AFF"),     // Blue
        LogLevel.Warning => Color.Parse("#FF9500"),  // Orange
        LogLevel.Error => Color.Parse("#FF3B30"),    // Red
        _ => Color.Parse("#8E8E93")
    };
}
