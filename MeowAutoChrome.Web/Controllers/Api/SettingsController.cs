using Microsoft.AspNetCore.Mvc;
using MeowAutoChrome.Web.Models;

namespace MeowAutoChrome.Web.Controllers.Api;

[ApiController]
[Route("api/settings")]
/// <summary>
/// 提供程序设置相关的 API，包括自动保存设置并触发运行时变更。<br/>
/// API for program settings, including autosave and applying runtime changes.
/// </summary>
public class SettingsController : ControllerBase
{
    private readonly Core.Interface.IProgramSettingsProvider programSettingsService;
    private readonly Core.Services.ScreencastServiceCore screencastService;
    private readonly Web.Services.BrowserInstanceManager browserInstances;

    /// <summary>
    /// 创建 SettingsController 实例。<br/>
    /// Create a SettingsController instance.
    /// </summary>
    /// <param name="programSettingsService">程序设置提供者 / program settings provider.</param>
    /// <param name="screencastService">投屏服务 / screencast service.</param>
    /// <param name="browserInstances">浏览器实例管理器 / browser instance manager.</param>
    public SettingsController(Core.Interface.IProgramSettingsProvider programSettingsService, Core.Services.ScreencastServiceCore screencastService, Web.Services.BrowserInstanceManager browserInstances)
    {
        this.programSettingsService = programSettingsService;
        this.screencastService = screencastService;
        this.browserInstances = browserInstances;
    }

    /// <summary>
    /// 接收来自前端的自动保存请求，验证并保存程序设置，必要时触发实例重建与投屏更新。<br/>
    /// Receive an autosave request from the UI, validate and save program settings, and apply changes.
    /// </summary>
    /// <param name="model">来自表单的视图模型 / view model from the form.</param>
    /// <returns>操作结果或错误信息 / operation result or error message.</returns>
    [HttpPost("autosave")]
    public async Task<IActionResult> AutoSave([FromForm] ProgramSettingsViewModel model)
    {
        // Basic validation similar to HomeController.ValidateProgramSettings
        if (!model.SearchUrlTemplate.Contains("{query}", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "搜索地址模板必须包含 {query} 占位符。" });

        try
        {
            var previousSettings = await programSettingsService.GetAsync();
            var settings = new Core.Struct.ProgramSettings
            {
                SearchUrlTemplate = model.SearchUrlTemplate,
                ScreencastFps = model.ScreencastFps,
                PluginPanelWidth = model.PluginPanelWidth,
                UserDataDirectory = model.UserDataDirectory,
                UserAgent = model.UserAgent,
                AllowInstanceUserAgentOverride = model.AllowInstanceUserAgentOverride,
                Headless = model.Headless
            };

            await programSettingsService.SaveAsync(settings);

            var userDataDirectoryChanged = !string.Equals(previousSettings.UserDataDirectory, settings.UserDataDirectory, StringComparison.OrdinalIgnoreCase);
            var headlessChanged = previousSettings.Headless != settings.Headless;
            var userAgentChanged = !string.Equals(previousSettings.UserAgent, settings.UserAgent, StringComparison.Ordinal);
            var userAgentOverrideChanged = previousSettings.AllowInstanceUserAgentOverride != settings.AllowInstanceUserAgentOverride;
            var launchSettingsChanged = userDataDirectoryChanged || headlessChanged || userAgentChanged || userAgentOverrideChanged;
            if (launchSettingsChanged)
                await browserInstances.UpdateLaunchSettingsAsync(settings.UserDataDirectory, settings.Headless, forceReload: true);

            await screencastService.UpdateSettingsAsync(
                screencastService.RequestedEnabled,
                screencastService.MaxWidth,
                screencastService.MaxHeight,
                Math.Max(16, (int)Math.Round(1000d / Math.Clamp(model.ScreencastFps, 1, 60))));

            if (launchSettingsChanged)
                await screencastService.OnBrowserModeChangedAsync();

            var changes = new List<string>();
            if (userDataDirectoryChanged)
                changes.Add("浏览器用户数据目录已切换");
            if (headlessChanged)
                changes.Add("Headless 模式已切换");
            if (userAgentChanged || userAgentOverrideChanged)
                changes.Add("User-Agent 配置已同步到实例");

            return Ok(new { message = changes.Count > 0 ? $"设置已自动保存，{string.Join('，', changes)}。" : "设置已自动保存。" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = ex.Message });
        }
    }
}
