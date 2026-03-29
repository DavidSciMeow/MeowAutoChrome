using Microsoft.AspNetCore.Mvc;
using MeowAutoChrome.Web.Models;

namespace MeowAutoChrome.Web.Controllers.Api;

[ApiController]
[Route("api/logs")]
public class LogsController : ControllerBase
{
    private readonly Core.Services.AppLogService appLogService;

    public LogsController(Core.Services.AppLogService appLogService)
    {
        this.appLogService = appLogService;
    }

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

    [HttpPost("clear")]
    public async Task<IActionResult> Clear()
    {
        await appLogService.ClearAsync();
        return Ok(new { cleared = true });
    }
}
