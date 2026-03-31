using Microsoft.Extensions.Logging;

namespace MeowAutoChrome.Core.Models;

/// <summary>
/// 应用日志条目模型，包含时间戳、级别、类别与消息。<br/>
/// Application log entry model containing timestamp, level, category and message.
/// </summary>
public sealed class AppLogEntry
{
    /// <summary>
    /// 默认构造函数，供序列化/反序列化使用。<br/>
    /// Default constructor used for serialization/deserialization.
    /// </summary>
    public AppLogEntry() { }

    /// <summary>
    /// 使用提供的详细信息创建新的日志条目。<br/>
    /// Create a new log entry with the provided details.
    /// </summary>
    /// <param name="timestamp">日志时间戳（含偏移）/ log timestamp (with offset).</param>
    /// <param name="level">日志级别 / log level.</param>
    /// <param name="category">日志类别或命名空间 / log category or namespace.</param>
    /// <param name="message">日志消息文本 / log message text.</param>
    public AppLogEntry(DateTimeOffset timestamp, LogLevel level, string category, string message)
    {
        Timestamp = timestamp;
        Level = level;
        Category = category;
        Message = message;
    }

    /// <summary>
    /// 日志时间戳（UTC 偏移）。<br/>
    /// Log timestamp (with offset).
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }
    /// <summary>
    /// 日志级别。<br/>
    /// Log level.
    /// </summary>
    public LogLevel Level { get; set; }
    /// <summary>
    /// 日志类别或命名空间。<br/>
    /// Log category or namespace.
    /// </summary>
    public string Category { get; set; } = string.Empty;
    /// <summary>
    /// 日志消息文本。<br/>
    /// Log message text.
    /// </summary>
    public string Message { get; set; } = string.Empty;
}
