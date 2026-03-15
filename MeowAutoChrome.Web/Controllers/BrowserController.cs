using MeowAutoChrome.Web.Hubs;
using MeowAutoChrome.Web.Models;
using MeowAutoChrome.Web.Services;
using MeowAutoChrome.Web.Warpper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace MeowAutoChrome.Web.Controllers
{
    public class BrowserController(BrowserInstanceManager browserInstances, IHubContext<BrowserHub> hub, ScreencastService screencastService, BrowserPluginHost pluginHost, ResourceMetricsService resourceMetricsService, ProgramSettingsService programSettingsService) : Controller
    {
        public IActionResult Index() => View();

        [HttpGet]
        public IActionResult Plugins()
            => Ok(pluginHost.GetPluginCatalog());

        [HttpPost]
        public async Task<IActionResult> ControlPlugin([FromBody] BrowserPluginControlRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.PluginId) || string.IsNullOrWhiteSpace(request.Command))
                return BadRequest();

            var result = await pluginHost.ControlAsync(request.PluginId, request.Command, request.Arguments, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> RunPluginFunction([FromBody] BrowserPluginFunctionExecutionRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.PluginId) || string.IsNullOrWhiteSpace(request.FunctionId))
                return BadRequest();

            var result = await pluginHost.ExecuteAsync(request.PluginId, request.FunctionId, request.Arguments, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }

        [HttpGet]
        public async Task<IActionResult> Status()
        {
            await screencastService.EnsureTargetAsync();
            return Ok(await BuildStatusAsync());
        }

        [HttpPost]
        public async Task<IActionResult> Navigate([FromBody] BrowserNavigateRequest request)
        {
            await browserInstances.NavigateAsync(request.Url);
            await screencastService.RefreshTargetAsync();
            return Ok(await BuildStatusAsync());
        }

        [HttpPost]
        public async Task<IActionResult> CloseTab([FromBody] BrowserCloseTabRequest request)
        {
            var closed = await browserInstances.CloseTabAsync(request.TabId);
            if (!closed)
                return NotFound();

            await screencastService.RefreshTargetAsync();
            return Ok(await BuildStatusAsync());
        }

        [HttpPost]
        public async Task<IActionResult> Back()
        {
            await browserInstances.GoBackAsync();
            return Ok(await BuildStatusAsync());
        }

        [HttpPost]
        public async Task<IActionResult> Forward()
        {
            await browserInstances.GoForwardAsync();
            return Ok(await BuildStatusAsync());
        }

        [HttpPost]
        public async Task<IActionResult> Reload()
        {
            await browserInstances.ReloadAsync();
            return Ok(await BuildStatusAsync());
        }

        [HttpPost]
        public async Task<IActionResult> NewTab()
        {
            await browserInstances.CreateTabAsync();
            await screencastService.RefreshTargetAsync();
            return Ok(await BuildStatusAsync());
        }

        [HttpPost]
        public async Task<IActionResult> SelectTab([FromBody] BrowserSelectTabRequest request)
        {
            var selected = await browserInstances.SelectPageAsync(request.TabId);
            if (!selected)
                return NotFound();

            await screencastService.RefreshTargetAsync();
            return Ok(await BuildStatusAsync());
        }

        [HttpPost]
        public async Task<IActionResult> Screencast([FromBody] ScreencastSettingsRequest request)
        {
            await screencastService.UpdateSettingsAsync(request.Enabled, request.MaxWidth, request.MaxHeight, request.FrameIntervalMs);
            return Ok(await BuildStatusAsync());
        }

        [HttpPost]
        public async Task<IActionResult> Layout([FromBody] BrowserLayoutSettingsRequest request)
        {
            var settings = await programSettingsService.GetAsync();
            settings.PluginPanelWidth = request.PluginPanelWidth;
            await programSettingsService.SaveAsync(settings);
            return Ok(new { pluginPanelWidth = settings.PluginPanelWidth });
        }

        [HttpGet]
        public async Task<IActionResult> Screenshot()
        {
            var screenshot = await browserInstances.CaptureScreenshotAsync();
            if (screenshot == null)
                return NotFound();

            return File(screenshot, "image/png");
        }

        private async Task<BrowserStatusResponse> BuildStatusAsync()
        {
            var metrics = resourceMetricsService.GetSnapshot();
            var settings = await programSettingsService.GetAsync();

            return new(
                browserInstances.CurrentUrl,
                await browserInstances.GetTitleAsync(),
                browserInstances.CurrentInstance.LastErrorMessage,
                browserInstances.IsHeadless,
                screencastService.Enabled,
                screencastService.MaxWidth,
                screencastService.MaxHeight,
                screencastService.FrameIntervalMs,
                metrics.CpuUsagePercent,
                metrics.MemoryUsageMb,
                browserInstances.TotalPageCount,
                settings.PluginPanelWidth,
                await browserInstances.GetTabsAsync());
        }
    }
}
