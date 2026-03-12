using System.ComponentModel.DataAnnotations;

namespace MeowAutoChrome.Web.Models;

public sealed class ProgramSettings
{
    public const string DefaultSearchUrlTemplate = "https://www.baidu.com/s?wd={query}";
    public const int DefaultScreencastFps = 10;
    public const int MinPluginPanelWidth = 240;
    public const int MaxPluginPanelWidth = 520;
    public const int DefaultPluginPanelWidth = 320;

    public string SearchUrlTemplate { get; set; } = DefaultSearchUrlTemplate;

    public int ScreencastFps { get; set; } = DefaultScreencastFps;

    public int PluginPanelWidth { get; set; } = DefaultPluginPanelWidth;
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
}
