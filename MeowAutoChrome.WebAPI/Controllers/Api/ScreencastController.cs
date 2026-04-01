using Microsoft.AspNetCore.Mvc;
using MeowAutoChrome.WebAPI.Models;
using MeowAutoChrome.Core.Services;

namespace MeowAutoChrome.WebAPI.Controllers.Api;

[ApiController]
[Route("api/screencast")]
/// <summary>
/// 推流配置 API，用于更新实时画面的传输参数。<br/>
/// Screencast configuration API used to update realtime frame delivery settings.
/// </summary>
public class ScreencastController : ControllerBase
{
    private readonly ScreencastServiceCore screencastService;

    public ScreencastController(ScreencastServiceCore screencastService)
    {
        this.screencastService = screencastService;
    }

    /// <summary>
    /// 更新实时画面推流设置。<br/>
    /// Update realtime screencast settings.
    /// </summary>
    /// <param name="request">推流设置请求。<br/>Screencast settings request.</param>
    /// <returns>更新结果。<br/>Update result.</returns>
    [HttpPost("settings")]
    public async Task<IActionResult> UpdateSettings([FromBody] ScreencastSettingsRequest request)
    {
        await screencastService.UpdateSettingsAsync(request.Enabled, request.MaxWidth, request.MaxHeight, request.FrameIntervalMs);
        return Ok(new { updated = true });
    }
}
