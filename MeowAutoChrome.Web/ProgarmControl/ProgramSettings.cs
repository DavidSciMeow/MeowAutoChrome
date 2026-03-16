namespace MeowAutoChrome.Web.ProgarmControl;

public sealed class ProgramSettings
{
    /// <summary>
    /// 默认的搜索 URL 模板，{query} 会被替换为搜索关键词。
    /// </summary>
    public const string DefaultSearchUrlTemplate = "https://www.baidu.com/s?wd={query}";
    /// <summary>
    /// 默认投屏帧率。
    /// </summary>
    public const int DefaultScreencastFps = 10;
    /// <summary>
    /// 插件区最小宽度。
    /// </summary>
    public const int MinPluginPanelWidth = 240;
    /// <summary>
    /// 插件区最大宽度。
    /// </summary>
    public const int MaxPluginPanelWidth = 520;
    /// <summary>
    /// 插件区默认宽度。
    /// </summary>
    public const int DefaultPluginPanelWidth = 320;

    /// <summary>
    /// 获取应用数据目录路径。
    /// </summary>
    public static string GetAppDataDirectoryPath()
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MeowAutoBrowser");

    /// <summary>
    /// 获取设置目录路径。
    /// </summary>
    public static string GetSettingsDirectoryPath()
        => Path.Combine(GetAppDataDirectoryPath(), "setting");

    /// <summary>
    /// 获取设置文件路径。
    /// </summary>
    public static string GetSettingsFilePath()
        => Path.Combine(GetSettingsDirectoryPath(), "program-settings.json");

    /// <summary>
    /// 获取旧版设置文件路径。
    /// </summary>
    public static string GetLegacySettingsFilePath()
        => Path.Combine(AppContext.BaseDirectory, "program-settings.json");

    /// <summary>
    /// 获取默认用户数据目录路径。
    /// </summary>
    public static string GetDefaultUserDataDirectoryPath()
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MeowAutoBrowser", "user");

    /// <summary>
    /// 获取旧版用户数据目录路径。
    /// </summary>
    public static string GetLegacyUserDataDirectoryPath()
        => Path.Combine(AppContext.BaseDirectory, "user_data");

    /// <summary>
    /// 搜索 URL 模板。
    /// </summary>
    public string SearchUrlTemplate { get; set; } = DefaultSearchUrlTemplate;

    /// <summary>
    /// 投屏帧率。
    /// </summary>
    public int ScreencastFps { get; set; } = DefaultScreencastFps;

    /// <summary>
    /// 插件区宽度。
    /// </summary>
    public int PluginPanelWidth { get; set; } = DefaultPluginPanelWidth;

    /// <summary>
    /// 用户数据目录。
    /// </summary>
    public string UserDataDirectory { get; set; } = GetDefaultUserDataDirectoryPath();

    /// <summary>
    /// 全局 User-Agent。
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// 是否允许实例覆盖全局 User-Agent。
    /// </summary>
    public bool AllowInstanceUserAgentOverride { get; set; }

    /// <summary>
    /// 是否启用 Headless 模式。
    /// </summary>
    public bool Headless { get; set; } = true;
}
