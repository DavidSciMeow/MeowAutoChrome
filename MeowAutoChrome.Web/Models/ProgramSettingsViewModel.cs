using MeowAutoChrome.Web.ProgarmControl;
using System.ComponentModel.DataAnnotations;

namespace MeowAutoChrome.Web.Models;

/// <summary>
/// 程序设置的视图模型
/// </summary>
public sealed class ProgramSettingsViewModel
{
    /// <summary>
    /// 搜索 URL 模板。
    /// </summary>
    [Required(ErrorMessage = "请输入搜索地址模板。")]
    public string SearchUrlTemplate { get; set; } = ProgramSettings.DefaultSearchUrlTemplate;

    /// <summary>
    /// 投屏帧率。
    /// </summary>
    [Range(1, 60, ErrorMessage = "目标 FPS 必须在 1 到 60 之间。")]
    public int ScreencastFps { get; set; } = ProgramSettings.DefaultScreencastFps;

    /// <summary>
    /// 插件区宽度。
    /// </summary>
    [Display(Name = "插件区宽度")]
    [Range(ProgramSettings.MinPluginPanelWidth, ProgramSettings.MaxPluginPanelWidth, ErrorMessage = "插件区宽度必须在 240 到 520 像素之间。")]
    public int PluginPanelWidth { get; set; } = ProgramSettings.DefaultPluginPanelWidth;

    /// <summary>
    /// 浏览器用户数据目录。
    /// </summary>
    [Display(Name = "浏览器用户数据目录")]
    [Required(ErrorMessage = "请输入浏览器用户数据目录。")]
    public string UserDataDirectory { get; set; } = ProgramSettings.GetDefaultUserDataDirectoryPath();

    /// <summary>
    /// 全局 User-Agent。
    /// </summary>
    [Display(Name = "全局 User-Agent")]
    public string? UserAgent { get; set; }

    /// <summary>
    /// 是否允许实例覆盖全局 User-Agent。
    /// </summary>
    [Display(Name = "允许实例覆盖全局 User-Agent")]
    public bool AllowInstanceUserAgentOverride { get; set; }

    /// <summary>
    /// 是否启用 Headless 模式。
    /// </summary>
    [Display(Name = "Headless 模式")]
    public bool Headless { get; set; } = true;
}
