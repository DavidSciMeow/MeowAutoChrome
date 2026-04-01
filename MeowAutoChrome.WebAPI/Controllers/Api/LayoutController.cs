using Microsoft.AspNetCore.Mvc;
using MeowAutoChrome.WebAPI.Models;
using MeowAutoChrome.Core.Interface;

namespace MeowAutoChrome.WebAPI.Controllers.Api;

[ApiController]
[Route("api/layout")]
/// <summary>
/// 布局保存 API，负责持久化桌面端界面布局参数。<br/>
/// Layout persistence API responsible for storing desktop UI layout settings.
/// </summary>
public class LayoutController : ControllerBase
{
    private readonly IProgramSettingsProvider programSettingsService;

    public LayoutController(IProgramSettingsProvider programSettingsService)
    {
        this.programSettingsService = programSettingsService;
    }

    /// <summary>
    /// 保存插件区宽度等布局设置。<br/>
    /// Save layout settings such as plugin panel width.
    /// </summary>
    /// <param name="request">布局设置请求。<br/>Layout settings request.</param>
    /// <returns>当前生效的布局值。<br/>Currently effective layout values.</returns>
    [HttpPost]
    public async Task<IActionResult> SaveLayout([FromBody] BrowserLayoutSettingsRequest request)
    {
        var settings = await programSettingsService.GetAsync();
        settings.PluginPanelWidth = request.PluginPanelWidth;
        await programSettingsService.SaveAsync(settings);
        return Ok(new { pluginPanelWidth = settings.PluginPanelWidth });
    }
}
