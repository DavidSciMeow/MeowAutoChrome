using MeowAutoChrome.Web.Models;
using MeowAutoChrome.Core.Interface;
using MeowAutoChrome.Core.Struct;

namespace MeowAutoChrome.Web.Services;

/// <summary>
/// Web 层的设置助手，负责验证与保存前端提交的程序设置，并将必要更改应用到运行时（例如重建实例与更新投屏设置）。<br/>
/// Web-layer helper for program settings: validate and persist UI-submitted settings and apply runtime changes (recreate instances, update screencast, etc.).
/// </summary>
public class SettingsService
{
    private readonly IProgramSettingsProvider programSettingsService;
    private readonly Core.Services.ScreencastServiceCore screencastService;
    private readonly BrowserInstanceManager browserInstances;

    /// <summary>
    /// 创建 SettingsService 实例。<br/>
    /// Create a SettingsService instance.
    /// </summary>
    public SettingsService(IProgramSettingsProvider programSettingsService, Core.Services.ScreencastServiceCore screencastService, BrowserInstanceManager browserInstances)
    {
        this.programSettingsService = programSettingsService;
        this.screencastService = screencastService;
        this.browserInstances = browserInstances;
    }

    private static int FpsToInterval(int fps)
        => Math.Max(16, (int)Math.Round(1000d / Math.Clamp(fps, 1, 60)));

    /// <summary>
    /// 校验前端的程序设置并通过回调返回模型错误（如果有）。<br/>
    /// Validate program settings from the UI and report model errors via the provided callback.
    /// </summary>
    /// <param name="model">来自表单的视图模型 / view model from the form.</param>
    /// <param name="addModelError">用于添加模型错误的回调 / callback to add model errors.</param>
    public void ValidateProgramSettings(ProgramSettingsViewModel model, Action<string> addModelError)
    {
        if (!model.SearchUrlTemplate.Contains("{query}", StringComparison.OrdinalIgnoreCase))
            addModelError("搜索地址模板必须包含 {query} 占位符。");

        try
        {
            var primary = browserInstances.CurrentInstance;
            if (primary is not null)
            {
                var currentUserDataDirectory = Path.GetFullPath(primary.UserDataDirectoryPath);
                var targetUserDataDirectory = Path.GetFullPath(model.UserDataDirectory);

                if (IsNestedDirectory(currentUserDataDirectory, targetUserDataDirectory) || IsNestedDirectory(targetUserDataDirectory, currentUserDataDirectory))
                    addModelError("浏览器用户数据目录不能设置为当前目录的子目录或父目录。");
            }

            if (model.CustomSettings is not null && model.CustomSettings.Count > 0)
            {
                var keyPattern = new System.Text.RegularExpressions.Regex("^[A-Za-z0-9_.-]+$");
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var kv in model.CustomSettings)
                {
                    var key = kv.Key?.Trim() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(key))
                    {
                        addModelError("自定义设置的键不能为空。请删除空行或填写键名。");
                        break;
                    }

                    if (!keyPattern.IsMatch(key))
                    {
                        addModelError($"自定义键 '{key}' 包含非法字符。允许的字符为字母、数字、下划线、破折号和点（A-Z a-z 0-9 _ - .）。");
                        break;
                    }

                    if (!seen.Add(key))
                    {
                        addModelError($"自定义键 '{key}' 重复。请删除或合并重复项。");
                        break;
                    }
                }
            }
        }
        catch (Exception)
        {
            addModelError("浏览器用户数据目录无效。");
        }
    }

    private static bool IsNestedDirectory(string parentPath, string childPath)
    {
        var normalizedParentPath = Path.TrimEndingDirectorySeparator(parentPath);
        var normalizedChildPath = Path.TrimEndingDirectorySeparator(childPath);
        return normalizedChildPath.StartsWith(normalizedParentPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || normalizedChildPath.StartsWith(normalizedParentPath + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 保存程序设置并在需要时应用运行时更改（例如重建实例或更新投屏设置）。<br/>
    /// Persist program settings and apply runtime changes when necessary (recreate instances, update screencast, etc.).
    /// </summary>
    /// <param name="model">来自 UI 的设置视图模型 / settings view model from the UI.</param>
    /// <returns>包含操作结果信息的字符串 / a string describing the result of the operation.</returns>
    public async Task<string> SaveProgramSettingsAsync(ProgramSettingsViewModel model)
    {
        var previousSettings = await programSettingsService.GetAsync();
        var settings = new ProgramSettings
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

        try
        {
            if (model.CustomSettings != null && model.CustomSettings.Count > 0)
            {
                await programSettingsService.InjectCustomSettingsAsync(model.CustomSettings);
            }
        }
        catch { }

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
            FpsToInterval(model.ScreencastFps));

        if (launchSettingsChanged)
            await screencastService.OnBrowserModeChangedAsync();

        var changes = new List<string>();
        if (userDataDirectoryChanged)
            changes.Add("浏览器用户数据目录已切换");
        if (headlessChanged)
            changes.Add("Headless 模式已切换");
        if (userAgentChanged || userAgentOverrideChanged)
            changes.Add("User-Agent 配置已同步到实例");

        if (changes.Count > 0)
            return $"设置已自动保存，{string.Join('，', changes)}。";

        return "设置已自动保存。";
    }
}
