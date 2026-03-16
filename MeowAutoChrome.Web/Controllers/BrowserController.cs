using MeowAutoChrome.Web.Hubs;
using MeowAutoChrome.Web.Models;
using MeowAutoChrome.Web.Services;
using MeowAutoChrome.Web.Warpper;
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
    public class BrowserController(BrowserInstanceManager browserInstances, IHubContext<BrowserHub> hub, ScreencastService screencastService, BrowserPluginHost pluginHost, ResourceMetricsService resourceMetricsService, ProgramSettingsService programSettingsService) : Controller
    {
        /// <summary>
        /// 用于从请求头中传递 BrowserHub 连接 ID 的自定义 header 名称，允许插件输出定向发送到特定客户端（例如仅发送给发起控制请求的页面）。如果未提供该 header，则插件输出将发送给所有连接的客户端。
        /// </summary>
        private const string BrowserHubConnectionIdHeaderName = "X-BrowserHub-ConnectionId";
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
        public async Task<IActionResult> ControlPlugin([FromBody] BrowserPluginControlRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.PluginId) || string.IsNullOrWhiteSpace(request.Command))
                return BadRequest();

            var result = await pluginHost.ControlAsync(request.PluginId, request.Command, request.Arguments, GetBrowserHubConnectionId(), cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        /// <summary>
        /// 执行插件的动作函数并返回结果。
        /// </summary>
        /// <param name="request">插件控制请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> RunPluginFunction([FromBody] BrowserPluginFunctionExecutionRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.PluginId) || string.IsNullOrWhiteSpace(request.FunctionId))
                return BadRequest();

            var result = await pluginHost.ExecuteAsync(request.PluginId, request.FunctionId, request.Arguments, GetBrowserHubConnectionId(), cancellationToken);
            return result is null ? NotFound() : Ok(result);
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
                return BadRequest();

            var settings = await browserInstances.GetInstanceSettingsAsync(instanceId);
            return settings is null ? NotFound() : Ok(settings);
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
                return BadRequest();

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
                return NotFound();

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
                return NotFound();

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
        /// 新建标签页。
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> NewTab()
        {
            await browserInstances.CreateTabAsync();
            await screencastService.RefreshTargetAsync();
            return Ok(await BuildStatusAsync());
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
                return NotFound();

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
            var screenshot = await browserInstances.CaptureScreenshotAsync();
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
