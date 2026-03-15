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

/// <summary>
/// 单条日志条目的视图模型，包含时间、级别、分类和消息文本等信息。
/// </summary>
public sealed class LogEntryViewModel
{
    /// <summary>
    /// 用于显示的时间文本（已格式化）。
    /// </summary>
    public string TimestampText { get; set; } = string.Empty;

    /// <summary>
    /// 用于显示的日志级别文本（例如 INFO、ERROR）。
    /// </summary>
    public string LevelText { get; set; } = string.Empty;

    /// <summary>
    /// 供过滤使用的日志级别字符串（与 LevelText 可能不同）。
    /// </summary>
    public string FilterLevel { get; set; } = string.Empty;

    /// <summary>
    /// 日志项所属的类别或来源（例如命名空间/组件名）。
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// 日志消息的主要文本内容。
    /// </summary>
    public string Message { get; set; } = string.Empty;
}
