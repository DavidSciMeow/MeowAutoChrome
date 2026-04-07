using MeowAutoChrome.Core.Services;
using MeowAutoChrome.WebAPI.Models;
using Microsoft.AspNetCore.Mvc;

namespace MeowAutoChrome.WebAPI.Controllers.Api;

[ApiController]
[Route("api/logs")]
/// <summary>
/// 程序日志 API，负责读取最近日志与清空日志文件。<br/>
/// Application log API for reading recent logs and clearing the log file.
/// </summary>
public class LogsController(AppLogService appLogService) : ControllerBase
{

    /// <summary>
    /// 读取最近日志内容。<br/>
    /// Read recent log content.
    /// </summary>
    /// <returns>适合日志页面展示的日志条目列表。<br/>Log entries formatted for the logs page.</returns>
    [HttpGet("content")]
    public async Task<IActionResult> Content()
    {
        var entries = (await appLogService.ReadRecentEntriesAsync()).Select(e => new LogEntryViewModel
        {
            TimestampText = e.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
            LevelText = e.Level.ToString(),
            FilterLevel = e.Level switch
            {
                LogLevel.Warning => "warn",
                LogLevel.Error or LogLevel.Critical => "error",
                LogLevel.Information => "info",
                LogLevel.Debug => "debug",
                _ => "trace"
            },
            Category = e.Category,
            Message = e.Message
        }).ToArray();

        return Ok(new
        {
            entries,
            lastUpdatedLocal = appLogService.GetLastWriteTime()?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")
        });
    }

    /// <summary>
    /// 清空日志。<br/>
    /// Clear the application log.
    /// </summary>
    /// <returns>清空结果。<br/>Clear result.</returns>
    [HttpPost("clear")]
    public async Task<IActionResult> Clear()
    {
        await appLogService.ClearAsync();
        return Ok(new { cleared = true });
    }
}
