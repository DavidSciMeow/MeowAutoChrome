namespace MeowAutoChrome.Web.Models;

/// <summary>
/// 表示应用程序日志条目。
/// </summary>
public sealed class AppLogEntry
{
    /// <summary>
    /// 初始化 <see cref="AppLogEntry"/> 类的新实例。
    /// </summary>
    public AppLogEntry()
    {
    }

    /// <summary>
    /// 使用指定参数初始化 <see cref="AppLogEntry"/> 类的新实例。
    /// </summary>
    /// <param name="timestamp">日志时间戳。</param>
    /// <param name="level">日志级别。</param>
    /// <param name="category">日志类别。</param>
    /// <param name="message">日志消息内容。</param>
    public AppLogEntry(DateTimeOffset timestamp, LogLevel level, string category, string message)
    {
        Timestamp = timestamp;
        Level = level;
        Category = category;
        Message = message;
    }

    /// <summary>
    /// 日志时间戳。
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// 日志级别。
    /// </summary>
    public LogLevel Level { get; set; }

    /// <summary>
    /// 日志类别。
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// 日志消息内容。
    /// </summary>
    public string Message { get; set; } = string.Empty;
}
