using Microsoft.AspNetCore.Mvc;
using MeowAutoChrome.Web.Models;
using MeowAutoChrome.Web.Services;

namespace MeowAutoChrome.Web.Controllers.Api
{
    /// <summary>
    /// 提供应用与当前浏览器状态的查询接口（用于前端状态轮询）。<br/>
    /// Provides endpoints to query application and current browser status (for UI polling).
    /// </summary>
    [ApiController]
    [Route("api/status")]
    public class StatusController : ControllerBase
    {
        private readonly BrowserInstanceManager browserInstances;
        private readonly Core.Services.ScreencastServiceCore screencastService;
        private readonly Core.Services.ResourceMetricsService resourceMetricsService;
        private readonly Core.Interface.IProgramSettingsProvider programSettingsService;

        /// <summary>
        /// 创建 StatusController 实例。<br/>
        /// Create a StatusController instance.
        /// </summary>
        /// <param name="browserInstances">浏览器实例管理服务 / browser instance manager service.</param>
        /// <param name="screencastService">投屏服务 / screencast service.</param>
        /// <param name="resourceMetricsService">资源度量服务 / resource metrics service.</param>
        /// <param name="programSettingsService">程序设置提供者 / program settings provider.</param>
        public StatusController(BrowserInstanceManager browserInstances, Core.Services.ScreencastServiceCore screencastService, Core.Services.ResourceMetricsService resourceMetricsService, Core.Interface.IProgramSettingsProvider programSettingsService)
        {
            this.browserInstances = browserInstances;
            this.screencastService = screencastService;
            this.resourceMetricsService = resourceMetricsService;
            this.programSettingsService = programSettingsService;
        }

        /// <summary>
        /// 获取应用及当前浏览器的运行时状态。<br/>
        /// Get runtime status for the application and current browser.
        /// </summary>
        /// <returns>包含浏览器状态的响应 / response containing browser status.</returns>
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            await screencastService.EnsureTargetAsync();
            return Ok(await BuildStatusAsync());
        }

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
}
