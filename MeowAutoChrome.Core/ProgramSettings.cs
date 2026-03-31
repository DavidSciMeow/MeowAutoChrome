namespace MeowAutoChrome.Core.Struct;

/// <summary>
/// 程序设置类，包含应用的全局配置项和相关路径获取方法。<br/>
/// Program settings class containing global application configuration and related path helpers.
/// 此类型已迁移到 Core 层，供其他模块直接引用。
/// </summary>
public sealed class ProgramSettings
{
    /// <summary>
    /// 默认搜索 URL 模板，{query} 将被替换为搜索词。<br/>
    /// Default search URL template where {query} will be replaced with the search term.
    /// </summary>
    public const string DefaultSearchUrlTemplate = "https://www.baidu.com/s?wd={query}";

    /// <summary>
    /// 默认屏幕投射帧率（FPS）。<br/>
    /// Default screencast frames per second (FPS).
    /// </summary>
    public const int DefaultScreencastFps = 10;

    /// <summary>
    /// 插件面板允许的最小宽度（像素）。<br/>
    /// Minimum plugin panel width in pixels.
    /// </summary>
    public const int MinPluginPanelWidth = 240;

    /// <summary>
    /// 插件面板允许的最大宽度（像素）。<br/>
    /// Maximum plugin panel width in pixels.
    /// </summary>
    public const int MaxPluginPanelWidth = 520;

    /// <summary>
    /// 默认插件面板宽度（像素）。<br/>
    /// Default plugin panel width in pixels.
    /// </summary>
    public const int DefaultPluginPanelWidth = 320;

    /// <summary>
    /// 获取应用数据目录路径（基于本地应用数据）。<br/>
    /// Get the application data directory path (based on LocalApplicationData).
    /// </summary>
    public static string GetAppDataDirectoryPath()
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MeowAutoChrome");

    /// <summary>
    /// 获取配置文件目录路径。<br/>
    /// Get the settings directory path.
    /// </summary>
    public static string GetSettingsDirectoryPath()
        => Path.Combine(GetAppDataDirectoryPath(), "setting");

    /// <summary>
    /// 获取程序设置文件的完整路径。<br/>
    /// Get the full path to the program settings file.
    /// </summary>
    public static string GetSettingsFilePath()
        => Path.Combine(GetSettingsDirectoryPath(), "program-settings.json");

    /// <summary>
    /// 获取旧版（可移植）设置文件路径。<br/>
    /// Get the legacy settings file path.
    /// </summary>
    public static string GetLegacySettingsFilePath()
        => Path.Combine(AppContext.BaseDirectory, "program-settings.json");

    /// <summary>
    /// 获取默认的用户数据目录路径（实例目录）。<br/>
    /// Get the default user data directory path for instances.
    /// </summary>
    public static string GetDefaultUserDataDirectoryPath()
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MeowAutoChrome", "instances");

    /// <summary>
    /// 获取默认插件目录路径（相对于应用基目录）。<br/>
    /// Get the default plugin directory path (relative to app base directory).
    /// </summary>
    public static string GetDefaultPluginDirectoryPath()
        => Path.Combine(AppContext.BaseDirectory, "Plugins");

    /// <summary>
    /// 获取旧版用户数据目录路径（兼容旧布局）。<br/>
    /// Get the legacy user data directory path (for backward compatibility).
    /// </summary>
    public static string GetLegacyUserDataDirectoryPath()
        => Path.Combine(AppContext.BaseDirectory, "user_data");

    /// <summary>
    /// 搜索 URL 模板（可自定义）。<br/>
    /// Search URL template (customizable).
    /// </summary>
    public string SearchUrlTemplate { get; set; } = DefaultSearchUrlTemplate;

    /// <summary>
    /// 屏幕投射帧率（FPS）。<br/>
    /// Screencast frames per second (FPS).
    /// </summary>
    public int ScreencastFps { get; set; } = DefaultScreencastFps;

    /// <summary>
    /// 插件面板当前宽度（像素）。<br/>
    /// Current plugin panel width in pixels.
    /// </summary>
    public int PluginPanelWidth { get; set; } = DefaultPluginPanelWidth;

    /// <summary>
    /// 默认的用户数据目录路径（实例使用）。<br/>
    /// Default user data directory path used for instances.
    /// </summary>
    public string UserDataDirectory { get; set; } = GetDefaultUserDataDirectoryPath();
    /// <summary>
    /// 插件根目录（用于在运行时发现并加载插件）。<br/>
    /// Plugin root directory used for runtime discovery and loading of plugins.
    /// </summary>
    public string PluginDirectory { get; set; } = GetDefaultPluginDirectoryPath();
    /// <summary>
    /// 默认的用户代理字符串（可为空）。<br/>
    /// Default user agent string (optional).
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// 是否允许单个实例覆盖 User-Agent 设置。<br/>
    /// Whether individual instances are allowed to override the User-Agent.
    /// </summary>
    public bool AllowInstanceUserAgentOverride { get; set; }

    /// <summary>
    /// 是否以无头模式运行（默认 true）。<br/>
    /// Whether to run in headless mode (default true).
    /// </summary>
    public bool Headless { get; set; } = true;
    /// <summary>
    /// 可供程序自定义扩展的键值配置集合，Web 或宿主可以通过 Contributor 注入自定义设置到此字典中。<br/>
    /// A key/value collection for program-level custom extensions; the Web or host can inject settings into this dictionary. Values should be strings for serialization compatibility.
    /// 字典中的值最好以字符串形式表示以保证序列化兼容性。
    /// </summary>
    public Dictionary<string, string?> CustomSettings { get; set; } = new();

    /// <summary>
    /// 每个插件/拥有者允许创建的最大浏览器实例数。零或负数表示不限制。<br/>
    /// Maximum number of browser instances a single plugin/owner may create. Zero or negative means unlimited.
    /// </summary>
    public int MaxInstancesPerPlugin { get; set; } = 3;

    /// <summary>
    /// 插件在请求实例时不允许传递的浏览器命令行参数列表。<br/>
    /// Disallowed browser arguments that plugins should not pass through when requesting instances.
    /// </summary>
    public string[] DisallowedBrowserArgs { get; set; } = new[] { "--user-data-dir", "--remote-debugging-port" };

    /// <summary>
    /// 上传文件的保留天数，超过该天数将自动清理。零或负数表示永久保留。<br/>
    /// Number of days to keep uploaded plugin files before automatic cleanup. Zero or negative means keep forever.
    /// </summary>
    public int UploadRetentionDays { get; set; } = 30;

    /// <summary>
    /// 单次上传中允许的最大文件数（目录上传按文件计数）。<br/>
    /// Maximum number of files allowed in a single upload (directory uploads count each file individually).
    /// </summary>
    public int MaxUploadFiles { get; set; } = 200;

    /// <summary>
    /// 单个上传文件允许的最大大小，单位为 MB。<br/>
    /// Maximum single uploaded file size in megabytes.
    /// </summary>
    public int MaxUploadFileSizeMb { get; set; } = 10;

    /// <summary>
    /// 单次上传允许包含的最大 DLL 数量。<br/>
    /// Maximum number of DLLs processed from an upload.
    /// </summary>
    public int MaxDllsPerUpload { get; set; } = 50;
}
