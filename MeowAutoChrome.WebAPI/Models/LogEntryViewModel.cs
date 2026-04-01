namespace MeowAutoChrome.WebAPI.Models;

/// <summary>
/// 日志条目视图模型。<br/>
/// View model representing one log entry.
/// </summary>
public sealed class LogEntryViewModel
{
    /// <summary>
    /// 本地时间文本。<br/>
    /// Local timestamp text.
    /// </summary>
    public string TimestampText { get; set; } = string.Empty;

    /// <summary>
    /// 日志级别文本。<br/>
    /// Log level text.
    /// </summary>
    public string LevelText { get; set; } = string.Empty;

    /// <summary>
    /// 前端筛选级别。<br/>
    /// Frontend filter level.
    /// </summary>
    public string FilterLevel { get; set; } = string.Empty;

    /// <summary>
    /// 日志分类。<br/>
    /// Log category.
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// 日志消息。<br/>
    /// Log message.
    /// </summary>
    public string Message { get; set; } = string.Empty;
}
