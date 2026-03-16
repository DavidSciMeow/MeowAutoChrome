namespace MeowAutoChrome.Web.Models;

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
