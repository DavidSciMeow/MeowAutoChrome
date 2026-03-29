using Microsoft.AspNetCore.Mvc;
using MeowAutoChrome.Web.Models;
using MeowAutoChrome.Web.Services;

namespace MeowAutoChrome.Web.Controllers.Api
{
    [ApiController]
    [Route("api/instances")]
    public class InstancesController : ControllerBase
    {
        private readonly BrowserInstanceManager browserInstances;
        private readonly Core.Services.ScreencastServiceCore screencastService;
        private readonly Core.Interface.IProgramSettingsProvider programSettingsService;

        public InstancesController(BrowserInstanceManager browserInstances, Core.Services.ScreencastServiceCore screencastService, Core.Interface.IProgramSettingsProvider programSettingsService)
        {
            this.browserInstances = browserInstances;
            this.screencastService = screencastService;
            this.programSettingsService = programSettingsService;
        }

        [HttpGet("settings")]
        public async Task<IActionResult> GetInstanceSettings([FromQuery] string instanceId)
        {
            if (string.IsNullOrWhiteSpace(instanceId))
                return BadRequest(new { error = "实例 ID 无效" });

            var settings = await browserInstances.GetInstanceSettingsAsync(instanceId);
            return settings is null ? NotFound(new { error = "实例不存在" }) : Ok(settings);
        }

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

        [HttpGet("preview")]
        public async Task<IActionResult> PreviewNewInstance([FromQuery] string? ownerPluginId, [FromQuery] string? userDataDirectoryRoot)
        {
            var owner = string.IsNullOrWhiteSpace(ownerPluginId) ? "ui" : ownerPluginId!;
            var preview = await browserInstances.PreviewNewInstanceAsync(owner, userDataDirectoryRoot);
            return Ok(new { instanceId = preview.InstanceId, userDataDirectory = preview.UserDataDirectory });
        }

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
        public record SetHeadlessRequest(bool IsHeadless);

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

        public record ViewportRequest(int Width, int Height);

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
