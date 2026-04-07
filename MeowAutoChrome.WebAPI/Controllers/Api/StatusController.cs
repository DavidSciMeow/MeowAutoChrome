using Microsoft.AspNetCore.Mvc;
using MeowAutoChrome.WebAPI.Models;
using MeowAutoChrome.WebAPI.Services;
using MeowAutoChrome.Core.Services;
using MeowAutoChrome.Core.Interface;

namespace MeowAutoChrome.WebAPI.Controllers.Api;

[ApiController]
[Route("api/status")]
/// <summary>
/// 浏览器状态查询 API，返回当前页面、实例、资源占用和推流状态。<br/>
/// Browser status API returning current page, instance, resource usage, and screencast state.
/// </summary>
public class StatusController(BrowserInstanceManager browserInstances, ScreencastServiceCore screencastService, ResourceMetricsService resourceMetricsService, IProgramSettingsProvider programSettingsService) : ControllerBase
{

    /// <summary>
    /// 读取当前浏览器运行状态快照。<br/>
    /// Read the current browser runtime status snapshot.
    /// </summary>
    /// <returns>包含当前 URL、标签页列表、资源指标和推流信息的响应。<br/>Response containing current URL, tab list, resource metrics, and screencast information.</returns>
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        await screencastService.EnsureTargetAsync();
        return Ok(await BuildStatusAsync());
    }

    /// <summary>
    /// 组装状态响应对象。<br/>
    /// Build the status response payload.
    /// </summary>
    /// <returns>供状态接口返回的聚合响应。<br/>Aggregated response returned by the status endpoint.</returns>
    private async Task<BrowserStatusResponse> BuildStatusAsync()
    {
        var metrics = resourceMetricsService.GetSnapshot();
        var settings = await programSettingsService.GetAsync();

        var hasInstance = browserInstances.GetInstances().Count > 0;
        var errorMessage = (string?)null;

        var supportsScreencast = hasInstance && browserInstances.IsHeadless;
        var screencastEnabled = supportsScreencast && screencastService.Enabled;

        return new(
            browserInstances.CurrentUrl,
            await browserInstances.GetTitleAsync(),
            errorMessage,
            supportsScreencast,
            screencastEnabled,
            screencastService.MaxWidth,
            screencastService.MaxHeight,
            screencastService.FrameIntervalMs,
            metrics.CpuUsagePercent,
            metrics.MemoryUsageMb,
            browserInstances.TotalPageCount,
            settings.PluginPanelWidth,
            await browserInstances.GetTabsAsync(),
            browserInstances.CurrentInstanceId,
            await browserInstances.GetCurrentInstanceViewportSettingsAsync(),
            browserInstances.IsHeadless);
    }
}
