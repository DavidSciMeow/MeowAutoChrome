using Microsoft.Extensions.Logging;

namespace MeowAutoChrome.Web.Models;

public sealed class AppLogEntry
{
    public DateTimeOffset Timestamp { get; set; }

    public LogLevel Level { get; set; }

    public string Category { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;
}
