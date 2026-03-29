using Microsoft.AspNetCore.Mvc;
using MeowAutoChrome.Web.Models;

namespace MeowAutoChrome.Web.Controllers.Api;

[ApiController]
[Route("api/settings")]
public class SettingsController : ControllerBase
{
    private readonly Core.Interface.IProgramSettingsProvider programSettingsService;
    private readonly Core.Services.ScreencastServiceCore screencastService;
    private readonly Web.Services.BrowserInstanceManager browserInstances;

    public SettingsController(Core.Interface.IProgramSettingsProvider programSettingsService, Core.Services.ScreencastServiceCore screencastService, Web.Services.BrowserInstanceManager browserInstances)
    {
        this.programSettingsService = programSettingsService;
        this.screencastService = screencastService;
        this.browserInstances = browserInstances;
    }

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
