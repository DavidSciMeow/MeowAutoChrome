using Microsoft.AspNetCore.Mvc;
using MeowAutoChrome.WebAPI.Models;
using MeowAutoChrome.WebAPI.Services;
using MeowAutoChrome.Core.Services;

namespace MeowAutoChrome.WebAPI.Controllers.Api;

[ApiController]
[Route("api/navigation")]
/// <summary>
/// 页面导航 API，负责导航、前进、后退和刷新。<br/>
/// Page navigation API handling navigate, forward, back, and reload actions.
/// </summary>
public class NavigationController(BrowserInstanceManager browserInstances, ScreencastServiceCore screencastService) : ControllerBase
{

    /// <summary>
    /// 导航到指定 URL。<br/>
    /// Navigate to the specified URL.
    /// </summary>
    /// <param name="request">导航请求。<br/>Navigation request.</param>
    /// <returns>导航结果。<br/>Navigation result.</returns>
    [HttpPost("navigate")]
    public async Task<IActionResult> Navigate([FromBody] BrowserNavigateRequest request)
    {
        await browserInstances.NavigateAsync(request.Url);
        await screencastService.RefreshTargetAsync();
        return Ok(new { navigated = true });
    }

    /// <summary>
    /// 在当前历史记录中后退。<br/>
    /// Navigate backward in the current history stack.
    /// </summary>
    /// <returns>操作结果。<br/>Operation result.</returns>
    [HttpPost("back")]
    public async Task<IActionResult> Back()
    {
        await browserInstances.GoBackAsync();
        return Ok(new { navigated = true });
    }

    /// <summary>
    /// 在当前历史记录中前进。<br/>
    /// Navigate forward in the current history stack.
    /// </summary>
    /// <returns>操作结果。<br/>Operation result.</returns>
    [HttpPost("forward")]
    public async Task<IActionResult> Forward()
    {
        await browserInstances.GoForwardAsync();
        return Ok(new { navigated = true });
    }

    /// <summary>
    /// 刷新当前页面。<br/>
    /// Reload the current page.
    /// </summary>
    /// <returns>操作结果。<br/>Operation result.</returns>
    [HttpPost("reload")]
    public async Task<IActionResult> Reload()
    {
        await browserInstances.ReloadAsync();
        return Ok(new { reloaded = true });
    }
}
