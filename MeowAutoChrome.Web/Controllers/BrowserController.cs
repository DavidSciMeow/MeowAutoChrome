using MeowAutoChrome.Web.Hubs;
using MeowAutoChrome.Contracts.BrowserContext;
using MeowAutoChrome.Web.Models;
using MeowAutoChrome.Web.Services;
using MeowAutoChrome.Contracts.Interface;
using MeowAutoChrome.Core.Services.PluginHost;
using MeowAutoChrome.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace MeowAutoChrome.Web.Controllers
{
    /// <summary>
    /// 提供浏览器相关的 HTTP API（导航、选项卡管理、Screencast 设置、插件控制等），被前端调用以控制后端浏览器实例。
    /// </summary>
    /// <param name="browserInstances">浏览器实例管理器</param>
    /// <param name="hub">SignalR Hub 上下文</param>
    /// <param name="screencastService">Screencast 服务</param>
    /// <param name="pluginHost">插件宿主</param>
    /// <param name="resourceMetricsService">资源监控服务</param>
    /// <param name="programSettingsService">程序设置服务</param>
public class BrowserController(IBrowserInstanceManager browserInstances, IHubContext<BrowserHub> hub, ScreencastService screencastService, MeowAutoChrome.Core.Interface.IPluginHostCore pluginHost, Core.Services.ResourceMetricsService resourceMetricsService, MeowAutoChrome.Core.Interface.IProgramSettingsProvider programSettingsService) : Controller
    {
        /// <summary>
        /// 用于从请求头中传递 BrowserHub 连接 ID 的自定义 header 名称，允许插件输出定向发送到特定客户端（例如仅发送给发起控制请求的页面）。如果未提供该 header，则插件输出将发送给所有连接的客户端。
        /// </summary>
        private const string BrowserHubConnectionIdHeaderName = "X-BrowserHub-ConnectionId";

        /// <summary>
        /// SignalR Hub 上下文
        /// </summary>
        public IHubContext<BrowserHub> Hub { get; } = hub;

        /// <summary>
        /// 浏览器页面的索引视图。
        /// </summary>
        public IActionResult Index() => View();
        /// <summary>
        /// 获取插件目录（供前端显示插件列表）。
        /// </summary>
        [HttpGet]
        public IActionResult Plugins()
            => Ok(pluginHost.GetPluginCatalog());
        /// <summary>
        /// 控制插件（start/stop/pause/resume）。
        /// </summary>
        /// <param name="request">插件控制请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> ControlPlugin([FromBody] Contracts.BrowserPlugin.BrowserPluginControlRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.PluginId) || string.IsNullOrWhiteSpace(request.Command))
                return Problem(detail: "插件控制请求参数无效", title: "InvalidRequest", statusCode: StatusCodes.Status400BadRequest);

            var result = await pluginHost.ControlAsync(request.PluginId, request.Command, request.Arguments, GetBrowserHubConnectionId(), cancellationToken);
            return result is null
                ? Problem(detail: "插件未找到或执行失败", title: "NotFound", statusCode: StatusCodes.Status404NotFound)
                : Ok((object?)result);
        }
        /// <summary>
        /// 执行插件的动作函数并返回结果。
        /// </summary>
        /// <param name="request">插件控制请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> RunPluginFunction([FromBody] Contracts.BrowserPlugin.BrowserPluginFunctionExecutionRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.PluginId) || string.IsNullOrWhiteSpace(request.FunctionId))
                return Problem(detail: "插件函数执行请求参数无效", title: "InvalidRequest", statusCode: StatusCodes.Status400BadRequest);

            var result = await pluginHost.ExecuteAsync(request.PluginId, request.FunctionId, request.Arguments, GetBrowserHubConnectionId(), cancellationToken);
            return result is null
                ? Problem(detail: "插件或函数未找到或执行失败", title: "NotFound", statusCode: StatusCodes.Status404NotFound)
                : Ok((object?)result);
        }
        /// <summary>
        /// 获取浏览器与系统状态（用于前端仪表盘）。
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Status()
        {
            await screencastService.EnsureTargetAsync();
            return Ok(await BuildStatusAsync());
        }
        /// <summary>
        /// 获取指定实例的设置。
        /// </summary>
        /// <param name="instanceId">实例 ID</param>
        /// <returns></returns>
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
        /// <summary>
        /// 导航当前页面到指定 URL 或关键字（将触发 Screencast 刷新）。
        /// </summary>
        /// <param name="request">导航请求</param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> Navigate([FromBody] BrowserNavigateRequest request)
        {
            await browserInstances.NavigateAsync(request.Url);
            await screencastService.RefreshTargetAsync();
            return Ok(await BuildStatusAsync());
        }
        /// <summary>
        /// 更新指定实例的设置。
        /// </summary>
        /// <param name="request">插件控制请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> InstanceSettings([FromBody] BrowserInstanceSettingsUpdateRequest request, CancellationToken cancellationToken)
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
                return Problem(detail: ex.Message, title: "InvalidOperation", statusCode: StatusCodes.Status400BadRequest);
            }

            if (!updated)
                return Problem(detail: "实例不存在", title: "NotFound", statusCode: StatusCodes.Status404NotFound);

            await screencastService.RefreshTargetAsync();
            return Ok(await BuildStatusAsync());
        }
        /// <summary>
        /// 同步当前实例的视口大小（通常由前端显示尺寸触发）。
        /// </summary>
        /// <param name="request">插件控制请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> CurrentInstanceViewport([FromBody] BrowserViewportSyncRequest request, CancellationToken cancellationToken)
        {
            if (request.Width <= 0 || request.Height <= 0)
                return Problem(detail: "宽高参数无效", title: "InvalidRequest", statusCode: StatusCodes.Status400BadRequest);

            await browserInstances.SyncCurrentInstanceViewportAsync(request.Width, request.Height, cancellationToken);
            await screencastService.RefreshTargetAsync();
            return Ok(new { synced = true });
        }
        /// <summary>
        /// 关闭指定标签页。
        /// </summary>
        /// <param name="request">插件控制请求</param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> CloseTab([FromBody] BrowserCloseTabRequest request)
        {
            var closed = await browserInstances.CloseTabAsync(request.TabId);
            if (!closed)
                return Problem(detail: "标签页不存在或已无法关闭", title: "NotFound", statusCode: StatusCodes.Status404NotFound);

            await screencastService.RefreshTargetAsync();
            return Ok(await BuildStatusAsync());
        }
        /// <summary>
        /// 关闭（或移除）指定的浏览器实例。
        /// </summary>
        /// <param name="request">插件控制请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> CloseInstance([FromBody] BrowserCloseInstanceRequest request, CancellationToken cancellationToken)
        {
            var closed = await browserInstances.CloseBrowserInstanceAsync(request.InstanceId, cancellationToken);
            if (!closed)
                return Problem(detail: "实例不存在或无法关闭", title: "NotFound", statusCode: StatusCodes.Status404NotFound);

            await screencastService.RefreshTargetAsync();
            return Ok(await BuildStatusAsync());
        }
        /// <summary>
        /// 页面后退操作。
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Back()
        {
            await browserInstances.GoBackAsync();
            return Ok(await BuildStatusAsync());
        }
        /// <summary>
        /// 页面前进操作。
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Forward()
        {
            await browserInstances.GoForwardAsync();
            return Ok(await BuildStatusAsync());
        }
        /// <summary>
        /// 重新加载当前页面。
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Reload()
        {
            await browserInstances.ReloadAsync();
            return Ok(await BuildStatusAsync());
        }
        /// <summary>
        /// 在指定实例或当前实例中新建标签页。
        /// 如果提供了 instanceId，会先切换到该实例再创建标签页。
        /// </summary>
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
                // 没有当前实例，创建一个新的实例然后返回状态
                var ownerPluginId = "ui";
                await browserInstances.CreateBrowserInstanceAsync(ownerPluginId);
                await screencastService.RefreshTargetAsync();
                return Ok(await BuildStatusAsync());
            }

            await browserInstances.CreateTabAsync(request.Url);
            await screencastService.RefreshTargetAsync();
            return Ok(await BuildStatusAsync());
        }

        /// <summary>
        /// 创建一个新的浏览器实例。
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateInstance([FromBody] BrowserCreateInstanceRequest request)
        {
            var ownerPluginId = string.IsNullOrWhiteSpace(request.OwnerPluginId) ? "ui" : request.OwnerPluginId;
            // determine userData root and instance id
            var settings = await programSettingsService.GetAsync();
            var userDataRoot = string.IsNullOrWhiteSpace(request.UserDataDirectory) ? settings.UserDataDirectory : request.UserDataDirectory!;

            string instanceId;
            // if user provided a display name but not a path, treat display name as folder name
            if (!string.IsNullOrWhiteSpace(request.DisplayName) && string.IsNullOrWhiteSpace(request.UserDataDirectory))
            {
                // create instance using DisplayName as id suffix
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
            // fetch instance settings so caller can show the exact user-data directory used
            var instSettings = await browserInstances.GetInstanceSettingsAsync(instanceId);
            await screencastService.RefreshTargetAsync();
            var status = await BuildStatusAsync();
            return Ok(new { instanceId, userDataDirectory = instSettings?.UserDataDirectory, status });
        }

        /// <summary>
        /// 预览将要创建的实例 ID 与 user-data 目录（不实际创建）。
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> PreviewNewInstance([FromQuery] string? ownerPluginId, [FromQuery] string? userDataDirectoryRoot)
        {
            var owner = string.IsNullOrWhiteSpace(ownerPluginId) ? "ui" : ownerPluginId!;
            var preview = await browserInstances.PreviewNewInstanceAsync(owner, userDataDirectoryRoot);
            return Ok(new { instanceId = preview.InstanceId, userDataDirectory = preview.UserDataDirectory });
        }

        /// <summary>
        /// Validate whether a folder name can be created under a root path.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ValidateInstanceFolder([FromBody] ValidateInstanceFolderRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.FolderName) || string.IsNullOrWhiteSpace(request.RootPath))
                return BadRequest(new ValidateInstanceFolderResponse(false, "Invalid input", null));

            try
            {
                var combined = Path.Combine(request.RootPath, request.FolderName);
                // try to create and delete directory as check
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
        /// <summary>
        /// 选择指定标签页为活动页。
        /// </summary>
        /// <param name="request">导航请求</param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> SelectTab([FromBody] BrowserSelectTabRequest request)
        {
            var selected = await browserInstances.SelectPageAsync(request.TabId);
            if (!selected)
                return Problem(detail: "标签页不存在", title: "NotFound", statusCode: StatusCodes.Status404NotFound);

            await screencastService.RefreshTargetAsync();
            return Ok(await BuildStatusAsync());
        }
        /// <summary>
        /// 更新 Screencast 设置。
        /// </summary>
        /// <param name="request">导航请求</param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> Screencast([FromBody] ScreencastSettingsRequest request)
        {
            await screencastService.UpdateSettingsAsync(request.Enabled, request.MaxWidth, request.MaxHeight, request.FrameIntervalMs);
            return Ok(await BuildStatusAsync());
        }
        /// <summary>
        /// 更新布局设置（例如插件面板宽度）。
        /// </summary>
        /// <param name="request">导航请求</param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> Layout([FromBody] BrowserLayoutSettingsRequest request)
        {
            var settings = await programSettingsService.GetAsync();
            settings.PluginPanelWidth = request.PluginPanelWidth;
            await programSettingsService.SaveAsync(settings);
            return Ok(new { pluginPanelWidth = settings.PluginPanelWidth });
        }
        /// <summary>
        /// 返回当前页面的截图 PNG（若存在活动页面）。
        /// </summary>
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
        /// <summary>
        /// 构建当前浏览器与系统状态响应对象。
        /// </summary>
        private async Task<BrowserStatusResponse> BuildStatusAsync()
        {
            var metrics = resourceMetricsService.GetSnapshot();
            var settings = await programSettingsService.GetAsync();

            var hasInstance = browserInstances.GetInstances().Count > 0;
            // Only expose an error message when an instance exists and reports one. Currently
            // the contract does not expose a LastErrorMessage; keep null for now.
            var errorMessage = (string?)null;

            // supportsScreencast should reflect whether the backend/browser can produce screencast frames.
            // Use presence of a BrowserContext (Playwright-managed) as capability indicator rather than
            // the headless flag. A Playwright-launched headful (non-headless) context can still provide
            // CDP screencast frames when launched by Playwright, so checking BrowserContext is more
            // accurate for front-end UI decisions.
            var supportsScreencast = hasInstance;
            var screencastEnabled = supportsScreencast && screencastService.Enabled;

            return new(
                browserInstances.CurrentUrl, // Updated to ensure clarity
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
                browserInstances.GetCurrentInstanceViewportSettings());
        }
        /// <summary>
        /// 从请求头中读取可选的 BrowserHub 连接 ID（用于将插件输出仅发送给特定客户端）。
        /// </summary>
        private string? GetBrowserHubConnectionId()
        {
            if (!Request.Headers.TryGetValue(BrowserHubConnectionIdHeaderName, out var values)) return null;
            var connectionId = values.FirstOrDefault()?.Trim();
            return string.IsNullOrWhiteSpace(connectionId) ? null : connectionId;
        }
    }
}
