using MeowAutoChrome.Web.Models;
using MeowAutoChrome.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace MeowAutoChrome.Web.Controllers
{
    // Keep route matching original controller route (e.g. /Browser/Action)
    [Route("Browser/[action]")]
    public class BrowserManagementController : Controller
    {
        private readonly BrowserInstanceManager browserInstances;
        private readonly Core.Services.ScreencastServiceCore screencastService;
        private readonly Core.Services.ResourceMetricsService resourceMetricsService;
        private readonly Core.Interface.IProgramSettingsProvider programSettingsService;
        private readonly Core.Interface.IPluginHostCore pluginHost;

        public BrowserManagementController(BrowserInstanceManager browserInstances, Core.Services.ScreencastServiceCore screencastService, Core.Services.ResourceMetricsService resourceMetricsService, Core.Interface.IProgramSettingsProvider programSettingsService, Core.Interface.IPluginHostCore pluginHost)
        {
            this.browserInstances = browserInstances;
            this.screencastService = screencastService;
            this.resourceMetricsService = resourceMetricsService;
            this.programSettingsService = programSettingsService;
            this.pluginHost = pluginHost;
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
                return Problem(detail: "实例 ID 无效", title: "InvalidRequest", statusCode: StatusCodes.Status400BadRequest);

            var settings = await browserInstances.GetInstanceSettingsAsync(instanceId);
            return settings is null
                ? Problem(detail: "实例不存在", title: "NotFound", statusCode: StatusCodes.Status404NotFound)
                : Ok(settings);
        }

        [HttpPost]
        public async Task<IActionResult> Navigate([FromBody] BrowserNavigateRequest request)
        {
            await browserInstances.NavigateAsync(request.Url);
            await screencastService.RefreshTargetAsync();
            return Ok(await BuildStatusAsync());
        }

        [HttpPost]
        public async Task<IActionResult> InstanceSettings([FromBody] Core.Models.BrowserInstanceSettingsUpdateRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.InstanceId)
                || string.IsNullOrWhiteSpace(request.UserDataDirectory)
                || request.ViewportWidth <= 0
                || request.ViewportHeight <= 0)
            {
                return Problem(detail: "请求参数无效", title: "InvalidRequest", statusCode: StatusCodes.Status400BadRequest);
            }

            bool updated;
            try
            {
                updated = await browserInstances.UpdateInstanceSettingsAsync(request, cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                return Problem(detail: ex.Message, title: "InvalidOperation", statusCode: StatusCodes.Status400BadRequest);
            }

            if (!updated)
                return Problem(detail: "实例不存在", title: "NotFound", statusCode: StatusCodes.Status404NotFound);

            await screencastService.RefreshTargetAsync();
            return Ok(await BuildStatusAsync());
        }

        [HttpPost]
        public async Task<IActionResult> CurrentInstanceViewport([FromBody] BrowserViewportSyncRequest request, CancellationToken cancellationToken)
        {
            if (request.Width <= 0 || request.Height <= 0)
                return Problem(detail: "宽高参数无效", title: "InvalidRequest", statusCode: StatusCodes.Status400BadRequest);

            await browserInstances.SyncCurrentInstanceViewportAsync(request.Width, request.Height, cancellationToken);
            await screencastService.RefreshTargetAsync();
            return Ok(new { synced = true });
        }

        [HttpPost]
        public async Task<IActionResult> CloseTab([FromBody] BrowserCloseTabRequest request)
        {
            var closed = await browserInstances.CloseTabAsync(request.TabId);
            if (!closed)
                return Problem(detail: "标签页不存在或已无法关闭", title: "NotFound", statusCode: StatusCodes.Status404NotFound);

            await screencastService.RefreshTargetAsync();
            return Ok(await BuildStatusAsync());
        }

        [HttpPost]
        public async Task<IActionResult> CloseInstance([FromBody] BrowserCloseInstanceRequest request, CancellationToken cancellationToken)
        {
            var closed = await browserInstances.CloseBrowserInstanceAsync(request.InstanceId, cancellationToken);
            if (!closed)
                return Problem(detail: "实例不存在或无法关闭", title: "NotFound", statusCode: StatusCodes.Status404NotFound);

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
        public async Task<IActionResult> NewTab([FromBody] BrowserCreateTabRequest request)
        {
            if (!string.IsNullOrWhiteSpace(request.InstanceId))
            {
                var selected = await browserInstances.SelectBrowserInstanceAsync(request.InstanceId);
                if (!selected)
                    return Problem(detail: "实例不存在", title: "NotFound", statusCode: StatusCodes.Status404NotFound);
            }

            if (browserInstances.GetInstances().Count == 0)
            {
                var ownerPluginId = "ui";
                await browserInstances.CreateBrowserInstanceAsync(ownerPluginId);
                await screencastService.RefreshTargetAsync();
                return Ok(await BuildStatusAsync());
            }

            await browserInstances.CreateTabAsync(request.Url);
            await screencastService.RefreshTargetAsync();
            return Ok(await BuildStatusAsync());
        }

        [HttpPost]
        public async Task<IActionResult> CreateInstance([FromBody] BrowserCreateInstanceRequest request)
        {
            var ownerPluginId = string.IsNullOrWhiteSpace(request.OwnerPluginId) ? "ui" : request.OwnerPluginId;
            var settings = await programSettingsService.GetAsync();
            var userDataRoot = string.IsNullOrWhiteSpace(request.UserDataDirectory) ? settings.UserDataDirectory : request.UserDataDirectory!;

            string instanceId;
            if (!string.IsNullOrWhiteSpace(request.DisplayName) && string.IsNullOrWhiteSpace(request.UserDataDirectory))
            {
                var previewId = request.DisplayName.Trim();
                instanceId = await browserInstances.CreateBrowserInstanceAsync(ownerPluginId, request.DisplayName ?? "Browser", userDataRoot, previewId);
            }
            else if (!string.IsNullOrWhiteSpace(request.PreviewInstanceId))
            {
                instanceId = await browserInstances.CreateBrowserInstanceAsync(ownerPluginId, request.DisplayName ?? "Browser", userDataRoot, request.PreviewInstanceId);
            }
            else
            {
                instanceId = await browserInstances.CreateBrowserInstanceAsync(ownerPluginId, request.DisplayName ?? "Browser", userDataRoot);
            }

            var instSettings = await browserInstances.GetInstanceSettingsAsync(instanceId);
            await screencastService.RefreshTargetAsync();
            var status = await BuildStatusAsync();
            return Ok(new { instanceId, userDataDirectory = instSettings?.UserDataDirectory, status });
        }

        [HttpGet]
        public async Task<IActionResult> PreviewNewInstance([FromQuery] string? ownerPluginId, [FromQuery] string? userDataDirectoryRoot)
        {
            var owner = string.IsNullOrWhiteSpace(ownerPluginId) ? "ui" : ownerPluginId!;
            var preview = await browserInstances.PreviewNewInstanceAsync(owner, userDataDirectoryRoot);
            return Ok(new { instanceId = preview.InstanceId, userDataDirectory = preview.UserDataDirectory });
        }

        [HttpPost]
        public async Task<IActionResult> ValidateInstanceFolder([FromBody] ValidateInstanceFolderRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.FolderName) || string.IsNullOrWhiteSpace(request.RootPath))
                return BadRequest(new ValidateInstanceFolderResponse(false, "Invalid input", null));

            try
            {
                var combined = Path.Combine(request.RootPath, request.FolderName);
                if (!Directory.Exists(request.RootPath))
                    Directory.CreateDirectory(request.RootPath);

                if (!Directory.Exists(combined))
                {
                    Directory.CreateDirectory(combined);
                    Directory.Delete(combined);
                }

                return Ok(new ValidateInstanceFolderResponse(true, null, combined));
            }
            catch (Exception ex)
            {
                return Ok(new ValidateInstanceFolderResponse(false, ex.Message, null));
            }
        }

        [HttpPost]
        public async Task<IActionResult> SelectTab([FromBody] BrowserSelectTabRequest request)
        {
            var selected = await browserInstances.SelectPageAsync(request.TabId);
            if (!selected)
                return Problem(detail: "标签页不存在", title: "NotFound", statusCode: StatusCodes.Status404NotFound);

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
            if (browserInstances.GetInstances().Count == 0)
                return Problem(detail: "无实例", title: "NoInstance", statusCode: StatusCodes.Status400BadRequest);

            var screenshot = await HttpContext.RequestServices.GetRequiredService<Core.Services.ScreenshotService>().CaptureScreenshotAsync();
            if (screenshot == null)
                return NotFound();

            return File(screenshot, "image/png");
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

        [HttpPost]
        public async Task<IActionResult> SetHeadless([FromBody] bool headless)
        {
            try
            {
                var settings = await programSettingsService.GetAsync();
                if (settings.Headless == headless)
                    return Ok(await BuildStatusAsync());

                var previousInstanceIds = browserInstances.GetInstances().Select(i => i.Id).ToArray();

                settings.Headless = headless;
                await programSettingsService.SaveAsync(settings);

                foreach (var id in previousInstanceIds)
                {
                    try { await pluginHost.CloseBrowserInstanceAsync(id); } catch { }
                }

                await browserInstances.UpdateLaunchSettingsAsync(settings.UserDataDirectory, settings.Headless, forceReload: true);
                await pluginHost.ScanPluginsAsync();
                await screencastService.OnBrowserModeChangedAsync();

                return Ok(await BuildStatusAsync());
            }
            catch (Exception ex)
            {
                return Problem(detail: ex.Message, title: "SetHeadlessFailed", statusCode: StatusCodes.Status500InternalServerError);
            }
        }
    }
}
