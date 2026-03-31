using Microsoft.AspNetCore.Mvc;
using MeowAutoChrome.Web.Models;

namespace MeowAutoChrome.Web.Controllers.Api;

/// <summary>
/// 提供应用日志读取与清除的 API。<br/>
/// API for reading and clearing application logs.
/// </summary>
[ApiController]
[Route("api/logs")]
public class LogsController : ControllerBase
{
    private readonly Core.Services.AppLogService appLogService;

    /// <summary>
    /// 创建一个新的 LogsController 实例。<br/>
    /// Create a new LogsController instance.
    /// </summary>
    /// <param name="appLogService">应用日志服务实例 / App log service instance.</param>
    public LogsController(Core.Services.AppLogService appLogService)
    {
        this.appLogService = appLogService;
    }

    /// <summary>
    /// 获取最近的日志条目与最后更新时间。<br/>
    /// Retrieve recent log entries and last updated timestamp.
    /// </summary>
    /// <returns>包含日志条目与最后更新时间的 IActionResult / IActionResult containing entries and last update time.</returns>
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
                _ => "info"
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
    /// 清除应用日志。<br/>
    /// Clear application logs.
    /// </summary>
    /// <returns>操作结果的 IActionResult / IActionResult for the operation result.</returns>
    [HttpPost("clear")]
    public async Task<IActionResult> Clear()
    {
        await appLogService.ClearAsync();
        return Ok(new { cleared = true });
    }
}
