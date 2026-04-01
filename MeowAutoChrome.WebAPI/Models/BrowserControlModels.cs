namespace MeowAutoChrome.WebAPI.Models;

/// <summary>
/// 创建浏览器实例请求。<br/>
/// Request used to create a browser instance.
/// </summary>
/// <param name="OwnerPluginId">实例所有者插件 ID。<br/>Owning plugin id.</param>
/// <param name="DisplayName">实例显示名称。<br/>Instance display name.</param>
/// <param name="UserDataDirectory">用户数据目录。<br/>User data directory.</param>
/// <param name="PreviewInstanceId">预览阶段生成的实例 ID。<br/>Preview-generated instance id.</param>
public sealed record BrowserCreateInstanceRequest(string? OwnerPluginId, string? DisplayName, string? UserDataDirectory, string? PreviewInstanceId);

/// <summary>
/// 创建标签页请求。<br/>
/// Request used to create a tab.
/// </summary>
/// <param name="InstanceId">可选的目标实例 ID。<br/>Optional target instance id.</param>
/// <param name="Url">初始 URL。<br/>Initial URL.</param>
public sealed record BrowserCreateTabRequest(string? InstanceId, string? Url);

/// <summary>
/// 创建实例响应。<br/>
/// Response returned after creating an instance.
/// </summary>
/// <param name="InstanceId">实例 ID。<br/>Instance id.</param>
/// <param name="UserDataDirectory">实例用户数据目录。<br/>Instance user data directory.</param>
public sealed record BrowserCreateInstanceResponse(string InstanceId, string? UserDataDirectory);

/// <summary>
/// 页面导航请求。<br/>
/// Request used to navigate a page.
/// </summary>
/// <param name="Url">目标 URL。<br/>Target URL.</param>
public sealed record BrowserNavigateRequest(string Url);

/// <summary>
/// 视口同步请求。<br/>
/// Request used to synchronize viewport size.
/// </summary>
/// <param name="Width">目标宽度。<br/>Target width.</param>
/// <param name="Height">目标高度。<br/>Target height.</param>
public sealed record BrowserViewportSyncRequest(int Width, int Height);

/// <summary>
/// 关闭标签页请求。<br/>
/// Request used to close a tab.
/// </summary>
/// <param name="TabId">标签页 ID。<br/>Tab id.</param>
public sealed record BrowserCloseTabRequest(string TabId);

/// <summary>
/// 关闭实例请求。<br/>
/// Request used to close an instance.
/// </summary>
/// <param name="InstanceId">实例 ID。<br/>Instance id.</param>
public sealed record BrowserCloseInstanceRequest(string InstanceId);

/// <summary>
/// 实例目录校验请求。<br/>
/// Request used to validate an instance folder.
/// </summary>
/// <param name="RootPath">目录根路径。<br/>Root directory path.</param>
/// <param name="FolderName">待校验目录名。<br/>Folder name to validate.</param>
public sealed record ValidateInstanceFolderRequest(string RootPath, string FolderName);

/// <summary>
/// 实例目录校验响应。<br/>
/// Response returned from instance folder validation.
/// </summary>
/// <param name="Ok">是否校验通过。<br/>Whether validation succeeded.</param>
/// <param name="Error">错误信息。<br/>Error message.</param>
/// <param name="Folder">完整目录路径。<br/>Full directory path.</param>
public sealed record ValidateInstanceFolderResponse(bool Ok, string? Error, string? Folder);

/// <summary>
/// 标签页选择请求。<br/>
/// Request used to select a tab.
/// </summary>
/// <param name="TabId">标签页 ID。<br/>Tab id.</param>
public sealed record BrowserSelectTabRequest(string TabId);

/// <summary>
/// 实时画面设置请求。<br/>
/// Request used to update screencast settings.
/// </summary>
/// <param name="Enabled">是否启用推流。<br/>Whether screencast is enabled.</param>
/// <param name="MaxWidth">最大宽度。<br/>Maximum width.</param>
/// <param name="MaxHeight">最大高度。<br/>Maximum height.</param>
/// <param name="FrameIntervalMs">帧间隔毫秒数。<br/>Frame interval in milliseconds.</param>
public sealed record ScreencastSettingsRequest(bool Enabled, int MaxWidth, int MaxHeight, int FrameIntervalMs);

/// <summary>
/// 布局设置请求。<br/>
/// Request used to persist layout settings.
/// </summary>
/// <param name="PluginPanelWidth">插件区宽度。<br/>Plugin panel width.</param>
public sealed record BrowserLayoutSettingsRequest(int PluginPanelWidth);

/// <summary>
/// 浏览器状态聚合响应。<br/>
/// Aggregated browser status response.
/// </summary>
/// <param name="CurrentUrl">当前 URL。<br/>Current URL.</param>
/// <param name="Title">当前标题。<br/>Current title.</param>
/// <param name="ErrorMessage">错误信息。<br/>Error message.</param>
/// <param name="SupportsScreencast">是否支持实时画面。<br/>Whether realtime screencast is supported.</param>
/// <param name="ScreencastEnabled">实时画面是否启用。<br/>Whether realtime screencast is enabled.</param>
/// <param name="ScreencastMaxWidth">推流最大宽度。<br/>Maximum screencast width.</param>
/// <param name="ScreencastMaxHeight">推流最大高度。<br/>Maximum screencast height.</param>
/// <param name="ScreencastFrameIntervalMs">推流帧间隔。<br/>Screencast frame interval.</param>
/// <param name="CpuUsagePercent">CPU 占用。<br/>CPU usage percent.</param>
/// <param name="MemoryUsageMb">内存占用。<br/>Memory usage in MB.</param>
/// <param name="TotalPageCount">页面总数。<br/>Total page count.</param>
/// <param name="PluginPanelWidth">插件区宽度。<br/>Plugin panel width.</param>
/// <param name="Tabs">标签页列表。<br/>Tab list.</param>
/// <param name="CurrentInstanceId">当前实例 ID。<br/>Current instance id.</param>
/// <param name="CurrentViewport">当前视口设置。<br/>Current viewport settings.</param>
/// <param name="IsHeadless">当前是否 Headless。<br/>Whether the current runtime is headless.</param>
public sealed record BrowserStatusResponse(
    string? CurrentUrl,
    string? Title,
    string? ErrorMessage,
    bool SupportsScreencast,
    bool ScreencastEnabled,
    int ScreencastMaxWidth,
    int ScreencastMaxHeight,
    int ScreencastFrameIntervalMs,
    double CpuUsagePercent,
    double MemoryUsageMb,
    int TotalPageCount,
    int PluginPanelWidth,
    IReadOnlyList<BrowserTabInfoDto> Tabs,
    string? CurrentInstanceId,
    BrowserInstanceViewportSettingsResponseDto CurrentViewport,
    bool IsHeadless);

/// <summary>
/// 标签页信息 DTO。<br/>
/// Tab information DTO.
/// </summary>
/// <param name="Id">标签页 ID。<br/>Tab id.</param>
/// <param name="Title">标题。<br/>Title.</param>
/// <param name="Url">URL。<br/>URL.</param>
/// <param name="IsActive">是否当前活动。<br/>Whether the tab is currently active.</param>
/// <param name="OwnerPluginId">所属插件 ID。<br/>Owning plugin id.</param>
/// <param name="InstanceId">所属实例 ID。<br/>Owning instance id.</param>
/// <param name="InstanceName">所属实例名称。<br/>Owning instance name.</param>
/// <param name="InstanceColor">所属实例颜色。<br/>Owning instance color.</param>
/// <param name="IsInSelectedInstance">是否属于当前选中实例。<br/>Whether the tab belongs to the currently selected instance.</param>
public sealed record BrowserTabInfoDto(
    string Id,
    string? Title,
    string? Url,
    bool IsActive,
    string? OwnerPluginId = null,
    string? InstanceId = null,
    string? InstanceName = null,
    string? InstanceColor = null,
    bool IsInSelectedInstance = false);
/// <summary>
/// 实例视口设置响应 DTO。<br/>
/// Instance viewport settings response DTO.
/// </summary>
/// <param name="Width">宽度。<br/>Width.</param>
/// <param name="Height">高度。<br/>Height.</param>
/// <param name="ViewportType">视口类型。<br/>Viewport type.</param>
public sealed record BrowserInstanceViewportSettingsResponseDto(int Width, int Height, string ViewportType = "Auto");
