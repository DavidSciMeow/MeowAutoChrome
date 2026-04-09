using System.Text;
using System.Text.Json;
using MeowAutoChrome.Core.Models;
using MeowAutoChrome.Core.Services;
using MeowAutoChrome.WebAPI.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace MeowAutoChrome.WebAPI.Services
{
    /// <summary>
    /// 后台服务：tail 日志文件并通过 SignalR 推送新增行。
    /// Background service that tails the app log file and broadcasts new lines via SignalR.
    /// </summary>
    public sealed class LogTailHostedService(AppLogService appLogService, IHubContext<LogHub> hub, ILogger<LogTailHostedService> logger) : BackgroundService
    {
        private readonly AppLogService _appLogService = appLogService;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var path = _appLogService.LogFilePath;
            long position = 0;

            try
            {
                if (File.Exists(path)) position = new FileInfo(path).Length;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "无法读取日志文件初始位置");
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (!File.Exists(path)) { await Task.Delay(500, stoppingToken); continue; }

                    using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    fs.Seek(position, SeekOrigin.Begin);
                    using var sr = new StreamReader(fs, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                    string? line;
                    while ((line = await sr.ReadLineAsync()) != null)
                    {
                        position = fs.Position;
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        AppLogEntry? entry = null;
                        try
                        {
                            entry = JsonSerializer.Deserialize<AppLogEntry>(line, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        }
                        catch { entry = null; }

                        if (entry is null)
                            continue;

                        var payload = new
                        {
                            TimestampText = entry.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                            LevelText = entry.Level.ToString(),
                            FilterLevel = entry.Level switch
                            {
                                LogLevel.Warning => "warn",
                                LogLevel.Error or LogLevel.Critical => "error",
                                LogLevel.Information => "info",
                                LogLevel.Debug => "debug",
                                _ => "trace"
                            },
                            entry.Category,
                            entry.Message
                        };

                        // broadcast to all connected clients
                        try
                        {
                            await hub.Clients.All.SendAsync("ReceiveLog", payload, stoppingToken);
                        }
                        catch (Exception ex)
                        {
                            logger.LogDebug(ex, "广播日志条目时出错");
                        }
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "读取日志时发生错误，稍后重试");
                    try { await Task.Delay(1000, stoppingToken); } catch { }
                }

                try { await Task.Delay(250, stoppingToken); } catch { }
            }
        }
    }
}
