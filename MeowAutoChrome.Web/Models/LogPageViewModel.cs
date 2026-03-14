namespace MeowAutoChrome.Web.Models;

public sealed class LogPageViewModel
{
    public string LogDisplayPath { get; set; } = string.Empty;

    public IReadOnlyList<LogEntryViewModel> Entries { get; set; } = [];

    public DateTimeOffset? LastUpdatedUtc { get; set; }
}

public sealed class LogEntryViewModel
{
    public string TimestampText { get; set; } = string.Empty;

    public string LevelText { get; set; } = string.Empty;

    public string FilterLevel { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;
}
