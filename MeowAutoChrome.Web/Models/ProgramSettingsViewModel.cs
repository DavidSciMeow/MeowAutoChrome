using MeowAutoChrome.Core.Struct;
using System.ComponentModel.DataAnnotations;

namespace MeowAutoChrome.Web.Models;

/// <summary>
/// 程序设置的视图模型，用于在 Web UI 中展示与编辑程序配置。<br/>
/// View model for program settings used to display and edit configuration in the Web UI.
/// </summary>
public sealed class ProgramSettingsViewModel
{
    /// <summary>
    /// 搜索 URL 模板。包含 {query} 占位符用于搜索替换。<br/>
    /// Search URL template. Should include the {query} placeholder for search substitution.
    /// </summary>
    [Required(ErrorMessage = "请输入搜索地址模板。")]
    public string SearchUrlTemplate { get; set; } = ProgramSettings.DefaultSearchUrlTemplate;

    /// <summary>
    /// <summary>
    /// 投屏帧率。<br/>
    /// Screencast frame rate.
    /// </summary>
    [Range(1, 60, ErrorMessage = "目标 FPS 必须在 1 到 60 之间。")]
    public int ScreencastFps { get; set; } = ProgramSettings.DefaultScreencastFps;

    /// <summary>
    /// 插件区宽度。<br/>
    /// Plugin panel width.
    /// </summary>
    [Display(Name = "插件区宽度")]
    [Range(ProgramSettings.MinPluginPanelWidth, ProgramSettings.MaxPluginPanelWidth, ErrorMessage = "插件区宽度必须在 240 到 520 像素之间。")]
    public int PluginPanelWidth { get; set; } = ProgramSettings.DefaultPluginPanelWidth;

    /// <summary>
    /// 浏览器用户数据目录。<br/>
    /// Browser user data directory.
    /// </summary>
    [Display(Name = "浏览器用户数据目录")]
    [Required(ErrorMessage = "请输入浏览器用户数据目录。")]
    public string UserDataDirectory { get; set; } = ProgramSettings.GetDefaultUserDataDirectoryPath();

    /// <summary>
    /// 全局 User-Agent。<br/>
    /// Global User-Agent string.
    /// </summary>
    [Display(Name = "全局 User-Agent")]
    public string? UserAgent { get; set; }

    /// <summary>
    /// 是否允许实例覆盖全局 User-Agent。<br/>
    /// Whether instances are allowed to override the global User-Agent.
    /// </summary>
    [Display(Name = "允许实例覆盖全局 User-Agent")]
    public bool AllowInstanceUserAgentOverride { get; set; }

    /// <summary>
    /// 是否启用 Headless 模式。<br/>
    /// Whether Headless mode is enabled.
    /// </summary>
    [Display(Name = "Headless 模式")]
    public bool Headless { get; set; } = true;
    /// <summary>
    /// 可选的自定义键值设置，由 Web UI 提供，注入到 Core 的 ProgramSettings.CustomSettings 中。<br/>
    /// Optional custom key/value settings provided by the Web UI and injected into Core's ProgramSettings.CustomSettings.
    /// </summary>
    public Dictionary<string, string?>? CustomSettings { get; set; }
}
