using System;
using System.IO;

namespace MeowAutoChrome.Core.Struct;

/// <summary>
/// 程序设置类，包含应用的全局配置项和相关路径获取方法。
/// 此类型已迁移到 Core 层，供其他模块直接引用。
/// </summary>
public sealed class ProgramSettings
{
    public const string DefaultSearchUrlTemplate = "https://www.baidu.com/s?wd={query}";
    public const int DefaultScreencastFps = 10;
    public const int MinPluginPanelWidth = 240;
    public const int MaxPluginPanelWidth = 520;
    public const int DefaultPluginPanelWidth = 320;

    public static string GetAppDataDirectoryPath()
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MeowAutoChrome");

    public static string GetSettingsDirectoryPath()
        => Path.Combine(GetAppDataDirectoryPath(), "setting");

    public static string GetSettingsFilePath()
        => Path.Combine(GetSettingsDirectoryPath(), "program-settings.json");

    public static string GetLegacySettingsFilePath()
        => Path.Combine(AppContext.BaseDirectory, "program-settings.json");

    public static string GetDefaultUserDataDirectoryPath()
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MeowAutoChrome", "instances");

    public static string GetDefaultPluginDirectoryPath()
        => Path.Combine(AppContext.BaseDirectory, "Plugins");

    public static string GetLegacyUserDataDirectoryPath()
        => Path.Combine(AppContext.BaseDirectory, "user_data");

    public string SearchUrlTemplate { get; set; } = DefaultSearchUrlTemplate;
    public int ScreencastFps { get; set; } = DefaultScreencastFps;
    public int PluginPanelWidth { get; set; } = DefaultPluginPanelWidth;
    public string UserDataDirectory { get; set; } = GetDefaultUserDataDirectoryPath();
    /// <summary>
    /// 插件根目录（用于在运行时发现并加载插件）。
    /// </summary>
    public string PluginDirectory { get; set; } = GetDefaultPluginDirectoryPath();
    public string? UserAgent { get; set; }
    public bool AllowInstanceUserAgentOverride { get; set; }
    public bool Headless { get; set; } = true;
    /// <summary>
    /// 可供程序自定义扩展的键值配置集合，Web 或宿主可以通过 Contributor 注入自定义设置到此字典中。
    /// 字典中的值最好以字符串形式表示以保证序列化兼容性。
    /// </summary>
    public System.Collections.Generic.Dictionary<string, string?> CustomSettings { get; set; } = new();
}
