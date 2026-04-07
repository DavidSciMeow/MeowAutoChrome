using Microsoft.AspNetCore.Mvc;
using MeowAutoChrome.WebAPI.Models;
using MeowAutoChrome.WebAPI.Services;
using MeowAutoChrome.Core.Services;
using MeowAutoChrome.Core.Interface;

namespace MeowAutoChrome.WebAPI.Controllers.Api;

[ApiController]
[Route("api/instances")]
/// <summary>
/// 浏览器实例管理 API，负责实例创建、关闭、设置读取与更新。<br/>
/// Browser instance management API for creating, closing, reading, and updating instances.
/// </summary>
public class InstancesController(BrowserInstanceManager browserInstances, ScreencastServiceCore screencastService, IProgramSettingsProvider programSettingsService) : ControllerBase
{

    /// <summary>
    /// 读取指定实例的设置快照。<br/>
    /// Read the settings snapshot for the specified instance.
    /// </summary>
    /// <param name="instanceId">目标实例 ID。<br/>Target instance id.</param>
    /// <returns>实例设置或错误响应。<br/>Instance settings or an error response.</returns>
    [HttpGet("settings")]
    public async Task<IActionResult> GetInstanceSettings([FromQuery] string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
            return BadRequest(new { error = "实例 ID 无效" });

        var settings = await browserInstances.GetInstanceSettingsAsync(instanceId);
        return settings is null ? NotFound(new { error = "实例不存在" }) : Ok(settings);
    }

    /// <summary>
    /// 更新指定实例的持久化设置。<br/>
    /// Update persisted settings for the specified instance.
    /// </summary>
    /// <param name="request">实例设置更新请求。<br/>Instance settings update request.</param>
    /// <param name="cancellationToken">请求取消令牌。<br/>Request cancellation token.</param>
    /// <returns>更新结果。<br/>Update result.</returns>
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
    /// 创建新的浏览器实例。<br/>
    /// Create a new browser instance.
    /// </summary>
    /// <param name="request">实例创建请求。<br/>Instance creation request.</param>
    /// <returns>新实例 ID 与用户数据目录。<br/>New instance id and user data directory.</returns>
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
        return Ok(new { instanceId, userDataDirectory = instSettings?.UserDataDirectory });
    }

    /// <summary>
    /// 预览新实例的默认实例 ID 与目录路径。<br/>
    /// Preview the default instance id and directory path for a new instance.
    /// </summary>
    /// <param name="ownerPluginId">实例所有者插件 ID。<br/>Owning plugin id.</param>
    /// <param name="userDataDirectoryRoot">用户数据目录根路径。<br/>User data directory root path.</param>
    /// <returns>预览结果。<br/>Preview result.</returns>
    [HttpGet("preview")]
    public async Task<IActionResult> PreviewNewInstance([FromQuery] string? ownerPluginId, [FromQuery] string? userDataDirectoryRoot)
    {
        var owner = string.IsNullOrWhiteSpace(ownerPluginId) ? "ui" : ownerPluginId!;
        var preview = await browserInstances.PreviewNewInstanceAsync(owner, userDataDirectoryRoot);
        return Ok(new { instanceId = preview.InstanceId, userDataDirectory = preview.UserDataDirectory });
    }

    /// <summary>
    /// 校验实例目录名在目标根目录下是否可用。<br/>
    /// Validate whether an instance folder name is usable under the target root directory.
    /// </summary>
    /// <param name="request">目录校验请求。<br/>Folder validation request.</param>
    /// <returns>校验结果。<br/>Validation result.</returns>
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
    /// 关闭指定浏览器实例。<br/>
    /// Close the specified browser instance.
    /// </summary>
    /// <param name="request">实例关闭请求。<br/>Instance close request.</param>
    /// <param name="cancellationToken">请求取消令牌。<br/>Request cancellation token.</param>
    /// <returns>关闭结果。<br/>Close result.</returns>
    [HttpPost("close")]
    public async Task<IActionResult> CloseInstance([FromBody] BrowserCloseInstanceRequest request, CancellationToken cancellationToken)
    {
        var closed = await browserInstances.CloseBrowserInstanceAsync(request.InstanceId, cancellationToken);
        if (!closed)
            return NotFound(new { error = "实例不存在或无法关闭" });

        await screencastService.RefreshTargetAsync();
        return Ok(new { closed = true });
    }

    /// <summary>
    /// Headless 模式切换请求。<br/>
    /// Request used to toggle headless mode.
    /// </summary>
    /// <param name="IsHeadless">是否启用 Headless。<br/>Whether headless mode should be enabled.</param>
    public record SetHeadlessRequest(bool IsHeadless);

    /// <summary>
    /// 更新全局浏览器运行模式。<br/>
    /// Update the global browser runtime mode.
    /// </summary>
    /// <param name="request">Headless 切换请求。<br/>Headless toggle request.</param>
    /// <returns>当前生效的 Headless 状态。<br/>Currently effective headless state.</returns>
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

        await browserInstances.UpdateLaunchSettingsAsync(settings.UserDataDirectory, settings.Headless, forceReload: true);
        await screencastService.RefreshTargetAsync();
        return Ok(new { headless = settings.Headless });
    }

    /// <summary>
    /// 视口同步请求。<br/>
    /// Viewport synchronization request.
    /// </summary>
    /// <param name="Width">目标宽度。<br/>Target width.</param>
    /// <param name="Height">目标高度。<br/>Target height.</param>
    public record ViewportRequest(int Width, int Height);

    /// <summary>
    /// 同步当前实例的视口大小。<br/>
    /// Synchronize the viewport size of the current instance.
    /// </summary>
    /// <param name="request">视口同步请求。<br/>Viewport synchronization request.</param>
    /// <returns>同步结果。<br/>Synchronization result.</returns>
    [HttpPost("viewport")]
    public async Task<IActionResult> UpdateCurrentInstanceViewport([FromBody] ViewportRequest request)
    {
        if (request is null || request.Width <= 0 || request.Height <= 0)
            return BadRequest(new { error = "请求参数无效" });

        await browserInstances.SyncCurrentInstanceViewportAsync(request.Width, request.Height);
        return Ok(new { updated = true });
    }
}
