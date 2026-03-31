using Microsoft.AspNetCore.Mvc;
using MeowAutoChrome.Web.Models;
using MeowAutoChrome.Web.Services;

namespace MeowAutoChrome.Web.Controllers.Api
{
    /// <summary>
    /// 管理浏览器实例的 API 控制器，提供获取/更新实例设置、创建/关闭实例等操作。<br/>
    /// API controller for managing browser instances: get/update settings, create/close instances, etc.
    /// </summary>
    [ApiController]
    [Route("api/instances")]
    public class InstancesController : ControllerBase
    {
        private readonly BrowserInstanceManager browserInstances;
        private readonly Core.Services.ScreencastServiceCore screencastService;
        private readonly Core.Interface.IProgramSettingsProvider programSettingsService;

        /// <summary>
        /// 创建实例控制器。<br/>
        /// Create an instances controller.
        /// </summary>
        /// <param name="browserInstances">浏览器实例管理器 / browser instance manager.</param>
        /// <param name="screencastService">投屏服务 / screencast service.</param>
        /// <param name="programSettingsService">程序设置提供者 / program settings provider.</param>
        public InstancesController(BrowserInstanceManager browserInstances, Core.Services.ScreencastServiceCore screencastService, Core.Interface.IProgramSettingsProvider programSettingsService)
        {
            this.browserInstances = browserInstances;
            this.screencastService = screencastService;
            this.programSettingsService = programSettingsService;
        }

        /// <summary>
        /// 获取指定实例的设置。<br/>
        /// Get settings for the specified instance.
        /// </summary>
        /// <param name="instanceId">实例 Id / instance id.</param>
        /// <returns>实例设置或错误响应 / instance settings or error response.</returns>
        [HttpGet("settings")]
        public async Task<IActionResult> GetInstanceSettings([FromQuery] string instanceId)
        {
            if (string.IsNullOrWhiteSpace(instanceId))
                return BadRequest(new { error = "实例 ID 无效" });

            var settings = await browserInstances.GetInstanceSettingsAsync(instanceId);
            return settings is null ? NotFound(new { error = "实例不存在" }) : Ok(settings);
        }

        /// <summary>
        /// 更新实例设置（例如视口尺寸与用户数据目录）。<br/>
        /// Update instance settings (viewport size, user data directory, etc.).
        /// </summary>
        /// <param name="request">包含更新信息的请求 / request with update information.</param>
        /// <param name="cancellationToken">取消令牌 / cancellation token.</param>
        /// <returns>操作结果或错误响应 / operation result or error response.</returns>
        [HttpPost("settings")]
        public async Task<IActionResult> UpdateInstanceSettings([FromBody] Core.Models.BrowserInstanceSettingsUpdateRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.InstanceId)
                || string.IsNullOrWhiteSpace(request.UserDataDirectory)
                || request.ViewportWidth <= 0
                || request.ViewportHeight <= 0)
            {
                return BadRequest(new { error = "请求参数无效" });
            }

            bool updated;
            try
            {
                updated = await browserInstances.UpdateInstanceSettingsAsync(request, cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }

            if (!updated)
                return NotFound(new { error = "实例不存在" });

            await screencastService.RefreshTargetAsync();
            return Ok(new { updated = true });
        }

        /// <summary>
        /// 创建新的浏览器实例并返回实例 Id。<br/>
        /// Create a new browser instance and return its id.
        /// </summary>
        /// <param name="request">包含创建所需信息的请求 / request with creation information.</param>
        /// <returns>包含新实例 Id 与用户数据目录的响应 / response containing instance id and user data directory.</returns>
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
            // return minimal info; Status endpoint available separately
            return Ok(new { instanceId, userDataDirectory = instSettings?.UserDataDirectory });
        }

        /// <summary>
        /// 预览新实例（生成临时实例 Id 与用户数据目录预览）。<br/>
        /// Preview a new instance (temporary instance id and user data directory preview).
        /// </summary>
        /// <param name="ownerPluginId">可选的拥有者插件 Id / optional owner plugin id.</param>
        /// <param name="userDataDirectoryRoot">可选的用户数据目录根路径 / optional user data directory root.</param>
        /// <returns>包含预览实例 Id 与用户数据目录的响应 / response with preview instance id and user data directory.</returns>
        [HttpGet("preview")]
        public async Task<IActionResult> PreviewNewInstance([FromQuery] string? ownerPluginId, [FromQuery] string? userDataDirectoryRoot)
        {
            var owner = string.IsNullOrWhiteSpace(ownerPluginId) ? "ui" : ownerPluginId!;
            var preview = await browserInstances.PreviewNewInstanceAsync(owner, userDataDirectoryRoot);
            return Ok(new { instanceId = preview.InstanceId, userDataDirectory = preview.UserDataDirectory });
        }

        /// <summary>
        /// 验证给定的实例文件夹名在根路径下是否有效。<br/>
        /// Validate whether a given instance folder name is valid under the root path.
        /// </summary>
        /// <param name="request">包含根路径与文件夹名的请求 / request containing root path and folder name.</param>
        /// <returns>包含验证结果与错误信息（如有）的响应 / response with validation result and optional error.</returns>
        [HttpPost("validate-folder")]
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

        /// <summary>
        /// 关闭指定的浏览器实例。<br/>
        /// Close the specified browser instance.
        /// </summary>
        /// <param name="request">包含要关闭实例 Id 的请求 / request containing the instance id to close.</param>
        /// <param name="cancellationToken">取消令牌 / cancellation token.</param>
        /// <returns>操作结果的 IActionResult / IActionResult for the operation result.</returns>
        [HttpPost("close")]
        public async Task<IActionResult> CloseInstance([FromBody] BrowserCloseInstanceRequest request, CancellationToken cancellationToken)
        {
            var closed = await browserInstances.CloseBrowserInstanceAsync(request.InstanceId, cancellationToken);
            if (!closed)
                return NotFound(new { error = "实例不存在或无法关闭" });

            await screencastService.RefreshTargetAsync();
            return Ok(new { closed = true });
        }

        // Toggle headless mode via API: updates program settings and triggers instance recreation
        /// <summary>
        /// 切换无头模式请求 DTO。<br/>
        /// DTO for toggling headless mode.
        /// </summary>
        public record SetHeadlessRequest(bool IsHeadless);

        /// <summary>
        /// 切换是否无头运行并应用更改。<br/>
        /// Toggle headless mode and apply changes.
        /// </summary>
        /// <param name="request">头部模式设置请求 / request for headless setting.</param>
        /// <returns>包含当前 headless 状态的 IActionResult / IActionResult with current headless state.</returns>
        [HttpPost("headless")]
        public async Task<IActionResult> SetHeadless([FromBody] SetHeadlessRequest request)
        {
            if (request is null)
                return BadRequest(new { error = "请求参数无效" });

            var settings = await programSettingsService.GetAsync();
            if (settings is null)
                return StatusCode(500, new { error = "程序设置不可用" });

            settings.Headless = request.IsHeadless;
            await programSettingsService.SaveAsync(settings);

            // Apply launch settings change (this will recreate instances if needed)
            await browserInstances.UpdateLaunchSettingsAsync(settings.UserDataDirectory, settings.Headless, forceReload: true);
            await screencastService.RefreshTargetAsync();
            return Ok(new { headless = settings.Headless });
        }

        /// <summary>
        /// 视口请求 DTO（宽度、高度）。<br/>
        /// Viewport request DTO (width, height).
        /// </summary>
        public record ViewportRequest(int Width, int Height);

        /// <summary>
        /// 更新当前实例的视口尺寸。<br/>
        /// Update the viewport size of the current instance.
        /// </summary>
        /// <param name="request">包含宽高的请求 / request containing width and height.</param>
        /// <returns>操作结果的 IActionResult / IActionResult for the operation result.</returns>
        [HttpPost("viewport")]
        public async Task<IActionResult> UpdateCurrentInstanceViewport([FromBody] ViewportRequest request)
        {
            if (request is null || request.Width <= 0 || request.Height <= 0)
                return BadRequest(new { error = "请求参数无效" });

            await browserInstances.SyncCurrentInstanceViewportAsync(request.Width, request.Height);
            return Ok(new { updated = true });
        }
    }
}
