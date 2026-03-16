using MeowAutoChrome.Web.Warpper;

namespace MeowAutoChrome.Web.Models;

/// <summary>
/// 导航请求模型，包含要导航到的 URL。
/// </summary>
/// <param name="Url">要导航到的目标 URL。</param>
public record BrowserNavigateRequest(string Url);

/// <summary>
/// 选择标签页请求，包含要选中的 TabId。
/// </summary>
/// <param name="TabId">要选中的标签页 ID。</param>
public record BrowserSelectTabRequest(string TabId);

/// <summary>
/// 关闭标签页请求，包含要关闭的 TabId。
/// </summary>
/// <param name="TabId">要关闭的标签页 ID。</param>
public record BrowserCloseTabRequest(string TabId);

/// <summary>
/// 关闭浏览器实例请求，包含要关闭的实例 ID。
/// </summary>
/// <param name="InstanceId">要关闭的浏览器实例 ID。</param>
public record BrowserCloseInstanceRequest(string InstanceId);

/// <summary>
/// Screencast 设置更新请求。
/// </summary>
/// <param name="Enabled">是否启用录屏。</param>
/// <param name="MaxWidth">录屏最大宽度。</param>
/// <param name="MaxHeight">录屏最大高度。</param>
/// <param name="FrameIntervalMs">录屏帧间隔（毫秒）。</param>
public record ScreencastSettingsRequest(bool Enabled, int MaxWidth, int MaxHeight, int FrameIntervalMs);

/// <summary>
/// 布局设置请求（例如插件面板宽度）。
/// </summary>
/// <param name="PluginPanelWidth">插件面板宽度。</param>
public record BrowserLayoutSettingsRequest(int PluginPanelWidth);

/// <summary>
/// 视口同步请求，前端传入显示区域的宽高以便实例自动调整视口。
/// </summary>
/// <param name="Width">显示区域宽度。</param>
/// <param name="Height">显示区域高度。</param>
public record BrowserViewportSyncRequest(int Width, int Height);

/// <summary>
/// 浏览器实例视口设置响应模型。
/// </summary>
/// <param name="Width">视口宽度。</param>
/// <param name="Height">视口高度。</param>
/// <param name="AutoResizeViewport">是否自动调整视口大小。</param>
/// <param name="PreserveAspectRatio">是否保持宽高比。</param>
public record BrowserInstanceViewportSettingsResponse(
    int Width,
    int Height,
    bool AutoResizeViewport,
    bool PreserveAspectRatio);

/// <summary>
/// 浏览器实例的 User-Agent 设置响应模型，包含程序级与实例级的 User-Agent 配置与状态。
/// </summary>
/// <param name="ProgramUserAgent">程序级 User-Agent。</param>
/// <param name="AllowInstanceOverride">是否允许实例覆盖 User-Agent。</param>
/// <param name="UseProgramUserAgent">是否使用程序级 User-Agent。</param>
/// <param name="UserAgent">实例自定义 User-Agent。</param>
/// <param name="EffectiveUserAgent">实际生效的 User-Agent。</param>
/// <param name="IsLocked">User-Agent 是否被锁定。</param>
public record BrowserInstanceUserAgentSettingsResponse(
    string? ProgramUserAgent,
    bool AllowInstanceOverride,
    bool UseProgramUserAgent,
    string? UserAgent,
    string? EffectiveUserAgent,
    bool IsLocked);

/// <summary>
/// 浏览器实例的完整设置响应模型。
/// </summary>
/// <param name="InstanceId">实例 ID。</param>
/// <param name="InstanceName">实例名称。</param>
/// <param name="UserDataDirectory">用户数据目录。</param>
/// <param name="IsSelected">是否为当前选中实例。</param>
/// <param name="Viewport">视口设置。</param>
/// <param name="UserAgent">User-Agent 设置。</param>
public record BrowserInstanceSettingsResponse(
    string InstanceId,
    string InstanceName,
    string UserDataDirectory,
    bool IsSelected,
    BrowserInstanceViewportSettingsResponse Viewport,
    BrowserInstanceUserAgentSettingsResponse UserAgent);

/// <summary>
/// 更新浏览器实例设置请求模型，包含实例 ID 和所有可更新的设置项（视口、User-Agent 等）。
/// </summary>
/// <param name="InstanceId">实例 ID。</param>
/// <param name="UserDataDirectory">用户数据目录。</param>
/// <param name="ViewportWidth">视口宽度。</param>
/// <param name="ViewportHeight">视口高度。</param>
/// <param name="AutoResizeViewport">是否自动调整视口大小。</param>
/// <param name="PreserveAspectRatio">是否保持宽高比。</param>
/// <param name="UseProgramUserAgent">是否使用程序级 User-Agent。</param>
/// <param name="UserAgent">自定义 User-Agent。</param>
/// <param name="MigrateExistingUserData">是否迁移已有用户数据。</param>
/// <param name="DisplayWidth">显示区域宽度。</param>
/// <param name="DisplayHeight">显示区域高度。</param>
public record BrowserInstanceSettingsUpdateRequest(
    string InstanceId,
    string UserDataDirectory,
    int ViewportWidth,
    int ViewportHeight,
    bool AutoResizeViewport,
    bool PreserveAspectRatio,
    bool UseProgramUserAgent,
    string? UserAgent,
    bool MigrateExistingUserData,
    int? DisplayWidth,
    int? DisplayHeight);

/// <summary>
/// 浏览器总体状态响应模型，用于前端定期获取浏览器与系统的运行状态。
/// </summary>
/// <param name="Url">连接URL</param>
/// <param name="Title">页面标题</param>
/// <param name="ErrorMessage">错误信息</param>
/// <param name="SupportsScreencast">是否支持录屏</param>
/// <param name="ScreencastEnabled">录屏是否启用</param>
/// <param name="ScreencastMaxWidth">录屏最大宽度</param>
/// <param name="ScreencastMaxHeight">录屏最大高度</param>
/// <param name="ScreencastFrameIntervalMs">录屏帧间隔（毫秒）</param>
/// <param name="CpuUsagePercent">CPU 使用率（百分比）</param>
/// <param name="MemoryUsageMb">内存使用量（MB）</param>
/// <param name="PageCount">页面数量</param>
/// <param name="PluginPanelWidth">插件面板宽度</param>
/// <param name="Tabs">标签页信息</param>
/// <param name="CurrentInstanceId">当前实例 ID</param>
/// <param name="CurrentInstanceViewport">当前实例视口设置</param>
public record BrowserStatusResponse(
    string? Url,
    string? Title,
    string? ErrorMessage,
    bool SupportsScreencast,
    bool ScreencastEnabled,
    int ScreencastMaxWidth,
    int ScreencastMaxHeight,
    int ScreencastFrameIntervalMs,
    double CpuUsagePercent,
    double MemoryUsageMb,
    int PageCount,
    int PluginPanelWidth,
    IReadOnlyList<BrowserTabInfo> Tabs,
    string CurrentInstanceId,
    BrowserInstanceViewportSettingsResponse CurrentInstanceViewport
);
