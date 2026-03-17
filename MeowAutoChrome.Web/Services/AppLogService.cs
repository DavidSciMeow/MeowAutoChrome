using System.Text;
using System.Text.Json;
using MeowAutoChrome.Web.Models;
using Microsoft.Extensions.Logging;

namespace MeowAutoChrome.Web.Services;

/// <summary>
/// 应用日志服务：负责写入、读取与清理本地日志文件。
/// </summary>
public sealed class AppLogService
{
    /// <summary>
    /// 默认最大日志条数，读取最近日志时的默认限制，防止一次性加载过多日志导致内存占用过高。
    /// </summary>
    private const int DefaultMaxEntries = 1000;
    /// <summary>
    /// 同步锁对象，确保在多线程环境下对日志文件的写入操作是线程安全的，避免竞争条件和数据损坏。
    /// </summary>
    private readonly object _syncRoot = new();
    /// <summary>
    /// 日志的物理目录路径，位于应用程序基目录下的 "logs" 文件夹中。确保日志文件与应用程序同目录，便于部署和访问，同时通过目录分隔保持文件系统整洁。
    /// </summary>
    private readonly string _logDirectoryPath = Path.Combine(AppContext.BaseDirectory, "logs");
    /// <summary>
    /// 日志的物理文件路径，位于应用程序基目录下的 "logs" 文件夹中，文件名为 "app.log"。确保日志文件与应用程序同目录，便于部署和访问，同时通过目录分隔保持文件系统整洁。
    /// </summary>
    private readonly string _logFilePath;
    /// <summary>
    /// JsonSerializerOptions 用于控制日志条目的序列化行为，确保输出紧凑且不包含多余的空白字符，以节省磁盘空间和提高写入性能。
    /// </summary>
    private static readonly JsonSerializerOptions JsonSerializerOptions = new() { WriteIndented = false };

    /// <summary>
    /// 初始化日志服务并确保日志目录存在。
    /// </summary>
    public AppLogService()
    {
        _logFilePath = Path.Combine(_logDirectoryPath, "app.log");
        Directory.CreateDirectory(_logDirectoryPath);
    }

    /// <summary>
    /// 日志文件的物理路径。
    /// </summary>
    public string LogFilePath => _logFilePath;

    /// <summary>
    /// 用于前端展示的日志相对路径。
    /// </summary>
    public string LogDisplayPath => Path.Combine("logs", "app.log").Replace('\\', '/');

    /// <summary>
    /// 写入一条结构化日志记录。
    /// </summary>
    /// <param name="entry">日志记录对象。</param>
    public void WriteEntry(AppLogEntry entry)
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
    /// 按级别与分类写入一条日志记录。
    /// </summary>
    /// <param name="level">日志级别。</param>
    /// <param name="message">日志消息。</param>
    /// <param name="category">日志分类。</param>
    public void WriteEntry(LogLevel level, string message, string category)
        => WriteEntry(new AppLogEntry
        {
            Timestamp = DateTimeOffset.Now,
            Level = level,
            Category = category,
            Message = message
        });

    /// <summary>
    /// 读取最近的日志记录。
    /// </summary>
    /// <param name="maxEntries">最多返回的日志条数。</param>
    /// <returns>最近日志记录集合。</returns>
    public async Task<IReadOnlyList<AppLogEntry>> ReadRecentEntriesAsync(int maxEntries = DefaultMaxEntries)
    {
        if (!File.Exists(_logFilePath))
            return [];

        await using var stream = new FileStream(_logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var entries = new Queue<AppLogEntry>();
        string? line;

        while ((line = await reader.ReadLineAsync()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            entries.Enqueue(ParseEntry(line));
            while (entries.Count > maxEntries)
                entries.Dequeue();
        }

        return [.. entries];
    }

    /// <summary>
    /// 清空日志文件内容。
    /// </summary>
    /// <returns>表示清理操作的任务。</returns>
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
    /// 获取日志文件最后写入时间（UTC）。
    /// </summary>
    /// <returns>最后写入时间；若文件不存在则返回 null。</returns>
    public DateTimeOffset? GetLastWriteTime()
    {
        if (!File.Exists(_logFilePath))
            return null;

        return File.GetLastWriteTimeUtc(_logFilePath);
    }

    /// <summary>
    /// 解析日志文件中的一行文本为 AppLogEntry 对象。首先尝试将文本作为 JSON 反序列化，如果失败则创建一个 Legacy 类型的日志条目，确保即使日志格式不正确也能保留原始信息。
    /// </summary>
    /// <param name="line">一条日志文本行。</param>
    /// <returns>解析后的 AppLogEntry 对象。</returns>
    private static AppLogEntry ParseEntry(string line)
    {
        try
        {
            return JsonSerializer.Deserialize<AppLogEntry>(line, JsonSerializerOptions) ?? CreateLegacyEntry(line);
        }
        catch
        {
            return CreateLegacyEntry(line);
        }
    }

    /// <summary>
    /// 获取一个 Legacy 类型的日志条目，用于表示无法解析为结构化日志的原始文本行。Legacy 类型的日志条目将原始文本保存在 Message 字段中，并使用默认的时间戳、信息级别和 "Legacy" 分类，以便区分这些条目与正常的结构化日志。
    /// </summary>
    /// <param name="line">一条日志文本行。</param>
    /// <returns>解析后的 AppLogEntry 对象。</returns>
    private static AppLogEntry CreateLegacyEntry(string line)
        => new()
        {
            Timestamp = DateTimeOffset.Now,
            Level = LogLevel.Information,
            Category = "Legacy",
            Message = line
        };
}
