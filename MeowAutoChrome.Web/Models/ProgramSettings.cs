using System.ComponentModel.DataAnnotations;

namespace MeowAutoChrome.Web.Models;

public sealed class ProgramSettings
{
    public const string DefaultSearchUrlTemplate = "https://www.baidu.com/s?wd={query}";
    public const int DefaultScreencastFps = 10;
    public const int MinPluginPanelWidth = 240;
    public const int MaxPluginPanelWidth = 520;
    public const int DefaultPluginPanelWidth = 320;

    public static string GetAppDataDirectoryPath()
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MeowAutoBrowser");

    public static string GetSettingsDirectoryPath()
        => Path.Combine(GetAppDataDirectoryPath(), "setting");

    public static string GetSettingsFilePath()
        => Path.Combine(GetSettingsDirectoryPath(), "program-settings.json");

    public static string GetLegacySettingsFilePath()
        => Path.Combine(AppContext.BaseDirectory, "program-settings.json");

    public static string GetDefaultUserDataDirectoryPath()
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MeowAutoBrowser", "user");

    public static string GetLegacyUserDataDirectoryPath()
        => Path.Combine(AppContext.BaseDirectory, "user_data");

    public string SearchUrlTemplate { get; set; } = DefaultSearchUrlTemplate;

    public int ScreencastFps { get; set; } = DefaultScreencastFps;

    public int PluginPanelWidth { get; set; } = DefaultPluginPanelWidth;

    public string UserDataDirectory { get; set; } = GetDefaultUserDataDirectoryPath();

    public string? UserAgent { get; set; }

    public bool AllowInstanceUserAgentOverride { get; set; }

    public bool Headless { get; set; } = true;
}

public sealed class ProgramSettingsViewModel
{
    [Required(ErrorMessage = "请输入搜索地址模板。")]
    public string SearchUrlTemplate { get; set; } = ProgramSettings.DefaultSearchUrlTemplate;

    [Range(1, 60, ErrorMessage = "目标 FPS 必须在 1 到 60 之间。")]
    public int ScreencastFps { get; set; } = ProgramSettings.DefaultScreencastFps;

    [Display(Name = "插件区宽度")]
    [Range(ProgramSettings.MinPluginPanelWidth, ProgramSettings.MaxPluginPanelWidth, ErrorMessage = "插件区宽度必须在 240 到 520 像素之间。")]
    public int PluginPanelWidth { get; set; } = ProgramSettings.DefaultPluginPanelWidth;

    [Display(Name = "浏览器用户数据目录")]
    [Required(ErrorMessage = "请输入浏览器用户数据目录。")]
    public string UserDataDirectory { get; set; } = ProgramSettings.GetDefaultUserDataDirectoryPath();

    [Display(Name = "全局 User-Agent")]
    public string? UserAgent { get; set; }

    [Display(Name = "允许实例覆盖全局 User-Agent")]
    public bool AllowInstanceUserAgentOverride { get; set; }

    [Display(Name = "Headless 模式")]
    public bool Headless { get; set; } = true;
}
