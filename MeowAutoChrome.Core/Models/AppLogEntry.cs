using Microsoft.Extensions.Logging;

namespace MeowAutoChrome.Core.Models;

public sealed class AppLogEntry
{
    public AppLogEntry() { }
    public AppLogEntry(DateTimeOffset timestamp, LogLevel level, string category, string message)
    {
        Timestamp = timestamp;
        Level = level;
        Category = category;
        Message = message;
    }

    public DateTimeOffset Timestamp { get; set; }
    public LogLevel Level { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
