using Microsoft.AspNetCore.Mvc;
using MeowAutoChrome.Web.Models;
using MeowAutoChrome.Web.Services;

namespace MeowAutoChrome.Web.Controllers.Api
{
    [ApiController]
    [Route("api/status")]
    public class StatusController : ControllerBase
    {
        private readonly BrowserInstanceManager browserInstances;
        private readonly Core.Services.ScreencastServiceCore screencastService;
        private readonly Core.Services.ResourceMetricsService resourceMetricsService;
        private readonly Core.Interface.IProgramSettingsProvider programSettingsService;

        public StatusController(BrowserInstanceManager browserInstances, Core.Services.ScreencastServiceCore screencastService, Core.Services.ResourceMetricsService resourceMetricsService, Core.Interface.IProgramSettingsProvider programSettingsService)
        {
            this.browserInstances = browserInstances;
            this.screencastService = screencastService;
            this.resourceMetricsService = resourceMetricsService;
            this.programSettingsService = programSettingsService;
        }

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
