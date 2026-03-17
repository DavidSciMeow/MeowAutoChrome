namespace MeowAutoChrome.Web.Models;

/// <summary>
/// 日志页面的视图模型，包含要显示的日志路径、日志条目列表以及最后更新时间。
/// </summary>
public sealed class LogPageViewModel
{
    /// <summary>
    /// 当前显示的日志文件或路径的可读描述。
    /// </summary>
    public string LogDisplayPath { get; set; } = string.Empty;

    /// <summary>
    /// 要显示的日志条目列表（按时间或页面要求排序）。
    /// </summary>
    public IReadOnlyList<LogEntryViewModel> Entries { get; set; } = [];

    /// <summary>
    /// 日志最后更新时间（UTC），用于前端显示或缓存控制。
    /// </summary>
    public DateTimeOffset? LastUpdatedUtc { get; set; }
}
