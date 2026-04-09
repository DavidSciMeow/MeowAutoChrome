using Microsoft.AspNetCore.Mvc;
using MeowAutoChrome.WebAPI.Models;
using MeowAutoChrome.WebAPI.Services;
using MeowAutoChrome.Core.Services;

namespace MeowAutoChrome.WebAPI.Controllers.Api;

[ApiController]
[Route("api/tabs")]
/// <summary>
/// 标签页管理 API，负责创建、关闭和切换当前标签页。<br/>
/// Tab management API for creating, closing, and switching the current tab.
/// </summary>
public class TabsController(BrowserInstanceManager browserInstances, ScreencastServiceCore screencastService) : ControllerBase
{

    /// <summary>
    /// 创建新的标签页；必要时可先切换到指定实例。<br/>
    /// Create a new tab and optionally switch to the specified instance first.
    /// </summary>
    /// <param name="request">标签页创建请求。<br/>Tab creation request.</param>
    /// <returns>创建结果。<br/>Creation result.</returns>
    [HttpPost("new")]
    public async Task<IActionResult> NewTab([FromBody] BrowserCreateTabRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.InstanceId))
        {
            var selected = await browserInstances.SelectBrowserInstanceAsync(request.InstanceId);
            if (!selected)
                return NotFound(new { error = "实例不存在" });
        }

        if (browserInstances.GetInstances().Count == 0)
        {
            var ownerPluginId = "ui";
            await browserInstances.CreateBrowserInstanceAsync(ownerPluginId);
            await screencastService.RefreshTargetAsync();
            return Ok(new { createdInstance = true });
        }

        await browserInstances.CreateTabAsync(request.Url);
        await screencastService.RefreshTargetAsync();
        return Ok(new { created = true });
    }

    /// <summary>
    /// 关闭指定标签页。<br/>
    /// Close the specified tab.
    /// </summary>
    /// <param name="request">标签页关闭请求。<br/>Tab close request.</param>
    /// <returns>关闭结果。<br/>Close result.</returns>
    [HttpPost("close")]
    public async Task<IActionResult> CloseTab([FromBody] BrowserCloseTabRequest request)
    {
        var closed = await browserInstances.CloseTabAsync(request.TabId);
        if (!closed)
            return NotFound(new { error = "标签页不存在或已无法关闭" });

        await screencastService.RefreshTargetAsync();
        return Ok(new { closed = true });
    }

    /// <summary>
    /// 选择指定标签页为当前活动页。<br/>
    /// Select the specified tab as the current active page.
    /// </summary>
    /// <param name="request">标签页选择请求。<br/>Tab selection request.</param>
    /// <returns>选择结果。<br/>Selection result.</returns>
    [HttpPost("select")]
    public async Task<IActionResult> SelectTab([FromBody] BrowserSelectTabRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TabId))
            return BadRequest(new { error = "tabId required" });

        var ok = await browserInstances.SelectPageAsync(request.TabId);
        if (!ok)
            return NotFound(new { error = "标签页不存在" });

        await screencastService.RefreshTargetAsync();
        return Ok(new { selected = true });
    }
}
