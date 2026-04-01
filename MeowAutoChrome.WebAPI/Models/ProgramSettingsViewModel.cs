using MeowAutoChrome.Core.Struct;
using System.ComponentModel.DataAnnotations;

namespace MeowAutoChrome.WebAPI.Models;

/// <summary>
/// 程序设置表单模型。<br/>
/// Form model representing editable program settings.
/// </summary>
public sealed class ProgramSettingsViewModel
{
    /// <summary>
    /// 搜索地址模板。<br/>
    /// Search URL template.
    /// </summary>
    [Required(ErrorMessage = "请输入搜索地址模板。")]
    public string SearchUrlTemplate { get; set; } = ProgramSettings.DefaultSearchUrlTemplate;

    /// <summary>
    /// 目标推流 FPS。<br/>
    /// Target screencast FPS.
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
    /// Global User-Agent.
    /// </summary>
    [Display(Name = "全局 User-Agent")]
    public string? UserAgent { get; set; }

    /// <summary>
    /// 是否允许实例覆盖全局 User-Agent。<br/>
    /// Whether instances may override the global User-Agent.
    /// </summary>
    [Display(Name = "允许实例覆盖全局 User-Agent")]
    public bool AllowInstanceUserAgentOverride { get; set; }

    /// <summary>
    /// 是否启用 Headless 模式。<br/>
    /// Whether headless mode is enabled.
    /// </summary>
    [Display(Name = "Headless 模式")]
    public bool Headless { get; set; } = true;

    /// <summary>
    /// 额外自定义设置。<br/>
    /// Additional custom settings.
    /// </summary>
    public Dictionary<string, string?>? CustomSettings { get; set; }
}
