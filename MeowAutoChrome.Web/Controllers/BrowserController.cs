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
        private const string BrowserHubConnectionIdHeaderName = "X-BrowserHub-ConnectionId";

        public IActionResult Index() => View();

        [HttpGet]
        public IActionResult Plugins()
            => Ok(pluginHost.GetPluginCatalog());

        [HttpPost]
        public async Task<IActionResult> ControlPlugin([FromBody] BrowserPluginControlRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.PluginId) || string.IsNullOrWhiteSpace(request.Command))
                return BadRequest();

            var result = await pluginHost.ControlAsync(request.PluginId, request.Command, request.Arguments, GetBrowserHubConnectionId(), cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> RunPluginFunction([FromBody] BrowserPluginFunctionExecutionRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.PluginId) || string.IsNullOrWhiteSpace(request.FunctionId))
                return BadRequest();

            var result = await pluginHost.ExecuteAsync(request.PluginId, request.FunctionId, request.Arguments, GetBrowserHubConnectionId(), cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }

        [HttpGet]
        public async Task<IActionResult> Status()
        {
            await screencastService.EnsureTargetAsync();
            return Ok(await BuildStatusAsync());
        }

        [HttpGet]
        public async Task<IActionResult> InstanceSettings([FromQuery] string instanceId)
        {
            if (string.IsNullOrWhiteSpace(instanceId))
                return BadRequest();

            var settings = await browserInstances.GetInstanceSettingsAsync(instanceId);
            return settings is null ? NotFound() : Ok(settings);
        }

        [HttpPost]
        public async Task<IActionResult> Navigate([FromBody] BrowserNavigateRequest request)
        {
            await browserInstances.NavigateAsync(request.Url);
            await screencastService.RefreshTargetAsync();
            return Ok(await BuildStatusAsync());
        }

        [HttpPost]
        public async Task<IActionResult> InstanceSettings([FromBody] BrowserInstanceSettingsUpdateRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.InstanceId)
                || string.IsNullOrWhiteSpace(request.UserDataDirectory)
                || request.ViewportWidth <= 0
                || request.ViewportHeight <= 0)
            {
                return BadRequest();
            }

            bool updated;
            try
            {
                updated = await browserInstances.UpdateInstanceSettingsAsync(
                    request.InstanceId,
                    request.UserDataDirectory,
                    request.ViewportWidth,
                    request.ViewportHeight,
                    request.AutoResizeViewport,
                    request.PreserveAspectRatio,
                    request.UseProgramUserAgent,
                    request.UserAgent,
                    request.MigrateExistingUserData,
                    request.DisplayWidth,
                    request.DisplayHeight,
                    cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }

            if (!updated)
                return NotFound();

            await screencastService.RefreshTargetAsync();
            return Ok(await BuildStatusAsync());
        }

        [HttpPost]
        public async Task<IActionResult> CurrentInstanceViewport([FromBody] BrowserViewportSyncRequest request, CancellationToken cancellationToken)
        {
            if (request.Width <= 0 || request.Height <= 0)
                return BadRequest();

            await browserInstances.SyncCurrentInstanceViewportAsync(request.Width, request.Height, cancellationToken);
            await screencastService.RefreshTargetAsync();
            return Ok(new { synced = true });
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
        public async Task<IActionResult> CloseInstance([FromBody] BrowserCloseInstanceRequest request, CancellationToken cancellationToken)
        {
            var closed = await browserInstances.CloseBrowserInstanceAsync(request.InstanceId, cancellationToken);
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
                await browserInstances.GetTabsAsync(),
                browserInstances.CurrentInstanceId,
                browserInstances.GetCurrentInstanceViewportSettings());
        }

        private string? GetBrowserHubConnectionId()
        {
            if (!Request.Headers.TryGetValue(BrowserHubConnectionIdHeaderName, out var values))
                return null;

            var connectionId = values.FirstOrDefault()?.Trim();
            return string.IsNullOrWhiteSpace(connectionId) ? null : connectionId;
        }
    }
}
