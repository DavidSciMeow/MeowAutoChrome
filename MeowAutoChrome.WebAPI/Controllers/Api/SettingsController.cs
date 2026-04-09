using Microsoft.AspNetCore.Mvc;
using MeowAutoChrome.WebAPI.Models;
using MeowAutoChrome.Core.Interface;
using MeowAutoChrome.Core.Services;
using MeowAutoChrome.Core.Struct;
using MeowAutoChrome.WebAPI.Services;

namespace MeowAutoChrome.WebAPI.Controllers.Api;

[ApiController]
[Route("api/settings")]
/// <summary>
/// 程序设置 API，负责读取与自动保存全局运行配置。<br/>
/// Program settings API for reading and autosaving global runtime configuration.
/// </summary>
public class SettingsController(IProgramSettingsProvider programSettingsService, ScreencastServiceCore screencastService, BrowserInstanceManager browserInstances, IPluginHostCore pluginHost) : ControllerBase
{

    /// <summary>
    /// 读取当前程序设置。<br/>
    /// Read the current program settings.
    /// </summary>
    /// <returns>前端设置页所需的配置快照。<br/>Configuration snapshot required by the settings page.</returns>
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var settings = await programSettingsService.GetAsync();
        // If plugin directory is missing or empty, set it to Core default and persist so clients see a valid path.
        if (string.IsNullOrWhiteSpace(settings.PluginDirectory))
        {
            settings.PluginDirectory = ProgramSettings.GetDefaultPluginDirectoryPath();
            try { await programSettingsService.SaveAsync(settings); } catch { }
        }
        return Ok(new
        {
            settings.SearchUrlTemplate,
            settings.ScreencastFps,
            settings.PluginPanelWidth,
            settings.UserDataDirectory,
            settings.UserAgent,
            settings.AllowInstanceUserAgentOverride,
            settings.Headless,
            settingsFilePath = ProgramSettings.GetSettingsFilePath(),
            defaultUserDataDirectory = ProgramSettings.GetDefaultUserDataDirectoryPath(),
            pluginDirectory = settings.PluginDirectory,
            defaultPluginDirectory = ProgramSettings.GetDefaultPluginDirectoryPath(),
            minPluginPanelWidth = ProgramSettings.MinPluginPanelWidth,
            maxPluginPanelWidth = ProgramSettings.MaxPluginPanelWidth
        });
    }

    /// <summary>
    /// 自动保存程序设置并同步必要的运行时状态。<br/>
    /// Autosave program settings and synchronize any runtime state that depends on them.
    /// </summary>
    /// <param name="model">设置表单模型。<br/>Settings form model.</param>
    /// <returns>保存结果与变更摘要。<br/>Save result and change summary.</returns>
    [HttpPost("autosave")]
    public async Task<IActionResult> AutoSave([FromForm] ProgramSettingsViewModel model)
    {
        if (!model.SearchUrlTemplate.Contains("{query}", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "搜索地址模板必须包含 {query} 占位符。" });

        try
        {
            var previousSettings = await programSettingsService.GetAsync();

            // If plugin directory changed, attempt to update/move first. Only allow single-directory changes.
            var pluginDirectoryChanged = !string.Equals(previousSettings.PluginDirectory, model.PluginDirectory, StringComparison.OrdinalIgnoreCase);
            if (pluginDirectoryChanged)
            {
                try
                {
                    await pluginHost.UpdatePluginRootPathAsync(model.PluginDirectory);
                }
                catch (ArgumentException aex)
                {
                    return BadRequest(new { message = aex.Message });
                }
                catch (Exception ex)
                {
                    return StatusCode(500, new { message = ex.Message });
                }
            }

            var settings = new Core.Struct.ProgramSettings
            {
                SearchUrlTemplate = model.SearchUrlTemplate,
                ScreencastFps = model.ScreencastFps,
                PluginPanelWidth = model.PluginPanelWidth,
                UserDataDirectory = model.UserDataDirectory,
                PluginDirectory = model.PluginDirectory,
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
            if (pluginDirectoryChanged)
                changes.Add("插件根目录已切换并重新扫描");
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

    /// <summary>
    /// 将所有程序设置重置为默认值并持久化。
    /// Reset all program settings to defaults and persist.
    /// </summary>
    [HttpPost("reset")]
    public async Task<IActionResult> Reset()
    {
        try
        {
            var defaultSettings = new Core.Struct.ProgramSettings();

            // Ensure plugin host uses the default plugin directory (may trigger migration)
            try
            {
                await pluginHost.UpdatePluginRootPathAsync(defaultSettings.PluginDirectory);
            }
            catch (ArgumentException aex)
            {
                return BadRequest(new { message = aex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }

            await programSettingsService.SaveAsync(defaultSettings);

            return Ok(new
            {
                defaultSettings.SearchUrlTemplate,
                defaultSettings.ScreencastFps,
                defaultSettings.PluginPanelWidth,
                defaultSettings.UserDataDirectory,
                defaultSettings.UserAgent,
                defaultSettings.AllowInstanceUserAgentOverride,
                defaultSettings.Headless,
                settingsFilePath = ProgramSettings.GetSettingsFilePath(),
                defaultUserDataDirectory = ProgramSettings.GetDefaultUserDataDirectoryPath(),
                pluginDirectory = defaultSettings.PluginDirectory,
                defaultPluginDirectory = ProgramSettings.GetDefaultPluginDirectoryPath(),
                minPluginPanelWidth = ProgramSettings.MinPluginPanelWidth,
                maxPluginPanelWidth = ProgramSettings.MaxPluginPanelWidth
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = ex.Message });
        }
    }
}
