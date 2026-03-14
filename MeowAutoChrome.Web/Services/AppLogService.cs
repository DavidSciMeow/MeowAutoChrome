using System.Text;
using System.Text.Json;
using MeowAutoChrome.Web.Models;
using Microsoft.Extensions.Logging;

namespace MeowAutoChrome.Web.Services;

public sealed class AppLogService
{
    private const int DefaultMaxEntries = 1000;
    private readonly object _syncRoot = new();
    private readonly string _logDirectoryPath = Path.Combine(AppContext.BaseDirectory, "logs");
    private readonly string _logFilePath;
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        WriteIndented = false
    };

    public AppLogService()
    {
        _logFilePath = Path.Combine(_logDirectoryPath, "app.log");
        Directory.CreateDirectory(_logDirectoryPath);
    }

    public string LogFilePath => _logFilePath;

    public string LogDisplayPath => Path.Combine("logs", "app.log").Replace('\\', '/');

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

    public void WriteEntry(LogLevel level, string message, string category)
        => WriteEntry(new AppLogEntry
        {
            Timestamp = DateTimeOffset.Now,
            Level = level,
            Category = category,
            Message = message
        });

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

    public Task ClearAsync()
    {
        lock (_syncRoot)
        {
            Directory.CreateDirectory(_logDirectoryPath);
            File.WriteAllText(_logFilePath, string.Empty, new UTF8Encoding(false));
        }

        return Task.CompletedTask;
    }

    public DateTimeOffset? GetLastWriteTime()
    {
        if (!File.Exists(_logFilePath))
            return null;

        return File.GetLastWriteTimeUtc(_logFilePath);
    }

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

    private static AppLogEntry CreateLegacyEntry(string line)
        => new()
        {
            Timestamp = DateTimeOffset.Now,
            Level = LogLevel.Information,
            Category = "Legacy",
            Message = line
        };
}
