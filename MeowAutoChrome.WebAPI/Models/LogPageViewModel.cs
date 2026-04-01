namespace MeowAutoChrome.WebAPI.Models;

/// <summary>
/// 日志页面视图模型。<br/>
/// View model representing the logs page.
/// </summary>
public sealed class LogPageViewModel
{
    /// <summary>
    /// 日志文件显示路径。<br/>
    /// Display path of the log file.
    /// </summary>
    public string LogDisplayPath { get; set; } = string.Empty;

    /// <summary>
    /// 日志条目集合。<br/>
    /// Collection of log entries.
    /// </summary>
    public IReadOnlyList<LogEntryViewModel> Entries { get; set; } = Array.Empty<LogEntryViewModel>();

    /// <summary>
    /// 最后更新时间。<br/>
    /// Last updated timestamp.
    /// </summary>
    public DateTimeOffset? LastUpdatedUtc { get; set; }
}
