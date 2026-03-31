using System.Text.Json;
using System.Text;
using Microsoft.Extensions.Logging;

namespace MeowAutoChrome.Core.Services;

/// <summary>
/// 应用日志服务，负责将日志项序列化并写入磁盘，以及读取最近日志项。<br/>
/// Application log service responsible for serializing log entries to disk and reading recent entries.
/// </summary>
public sealed class AppLogService
{
    private const int DefaultMaxEntries = 1000;
    private readonly object _syncRoot = new();
    private readonly string _logDirectoryPath = Path.Combine(AppContext.BaseDirectory, "logs");
    private readonly string _logFilePath;
    private static readonly JsonSerializerOptions JsonSerializerOptions = new() { WriteIndented = false };

    /// <summary>
    /// 构造函数：初始化日志文件路径并确保日志目录存在。<br/>
    /// Constructor: initializes the log file path and ensures the log directory exists.
    /// </summary>
    public AppLogService()
    {
        _logFilePath = Path.Combine(_logDirectoryPath, "app.log");
        Directory.CreateDirectory(_logDirectoryPath);
    }

    /// <summary>
    /// 日志文件在磁盘上的完整路径。<br/>
    /// Full filesystem path to the log file.
    /// </summary>
    public string LogFilePath => _logFilePath;

    /// <summary>
    /// 日志文件在 Web 展示时使用的相对显示路径（forward-slash）。<br/>
    /// Relative display path for the log file (forward-slash) used by the web UI.
    /// </summary>
    public string LogDisplayPath => Path.Combine("logs", "app.log").Replace('\\', '/');

    /// <summary>
    /// 将给定的日志项写入日志文件。<br/>
    /// Write the provided log entry to the log file.
    /// </summary>
    /// <param name="entry">要写入的日志项 / log entry to write.</param>
    public void WriteEntry(Models.AppLogEntry entry)
    {
        entry.Timestamp = entry.Timestamp == default ? DateTimeOffset.Now : entry.Timestamp;
        entry.Category = string.IsNullOrWhiteSpace(entry.Category) ? "Application" : entry.Category.Trim();
        entry.Message = entry.Message ?? string.Empty;

        var serializedEntry = JsonSerializer.Serialize(entry, JsonSerializerOptions);

        lock (_syncRoot)
        {
            Directory.CreateDirectory(_logDirectoryPath);
            File.AppendAllText(_logFilePath, serializedEntry + Environment.NewLine, new UTF8Encoding(false));
        }
    }

    /// <summary>
    /// 使用指定日志级别、消息和类别创建并写入日志项的便捷方法。<br/>
    /// Convenience helper to create and write a log entry with the specified level, message and category.
    /// </summary>
    /// <param name="level">日志级别 / log level.</param>
    /// <param name="message">日志消息 / message.</param>
    /// <param name="category">日志类别 / category.</param>
    public void WriteEntry(LogLevel level, string message, string category)
        => WriteEntry(new Models.AppLogEntry(DateTimeOffset.Now, level, category, message));

    /// <summary>
    /// 读取最近的日志条目。若日志文件不存在则返回空集合。<br/>
    /// Read recent log entries; returns an empty collection when the log file does not exist.
    /// </summary>
    /// <param name="maxEntries">要返回的最大条目数 / maximum number of entries to return.</param>
    /// <returns>最近日志项的只读列表 / read-only list of recent log entries.</returns>
    public async Task<IReadOnlyList<Models.AppLogEntry>> ReadRecentEntriesAsync(int maxEntries = DefaultMaxEntries)
    {
        if (!File.Exists(_logFilePath))
            return Array.Empty<Models.AppLogEntry>();

        await using var stream = new FileStream(_logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var entries = new Queue<Models.AppLogEntry>();
        string? line;

        while ((line = await reader.ReadLineAsync()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            entries.Enqueue(ParseEntry(line));
            while (entries.Count > maxEntries)
                entries.Dequeue();
        }

        return entries.ToArray();
    }

    /// <summary>
    /// 清空日志文件内容（覆盖为空）。<br/>
    /// Clear the log file contents (overwrite with empty content).
    /// </summary>
    /// <returns>完成时的任务 / task that completes when the clear operation is done.</returns>
    public Task ClearAsync()
    {
        lock (_syncRoot)
        {
            Directory.CreateDirectory(_logDirectoryPath);
            File.WriteAllText(_logFilePath, string.Empty, new UTF8Encoding(false));
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 获取日志文件的最近写入时间（UTC），如果文件不存在则返回 null。<br/>
    /// Get the last write time (UTC) of the log file, or null if the file does not exist.
    /// </summary>
    /// <returns>日志文件的最近写入时间或 null / last write time of the log file or null.</returns>
    public DateTimeOffset? GetLastWriteTime()
    {
        if (!File.Exists(_logFilePath))
            return null;

        return File.GetLastWriteTimeUtc(_logFilePath);
    }

    private static Models.AppLogEntry ParseEntry(string line)
    {
        try
        {
            return JsonSerializer.Deserialize<Models.AppLogEntry>(line, JsonSerializerOptions) ?? CreateLegacyEntry(line);
        }
        catch
        {
            return CreateLegacyEntry(line);
        }
    }

    private static Models.AppLogEntry CreateLegacyEntry(string line)
        => new(DateTimeOffset.Now, LogLevel.Information, "Legacy", line);
}
