namespace MeowAutoChrome.Web.Models;

/// <summary>
/// 请求创建浏览器实例的 DTO，包含可选的插件拥有者与显示名称等信息。<br/>
/// Request DTO for creating a browser instance, including optional owner plugin id and display name.
/// </summary>
/// <param name="OwnerPluginId">可选的拥有者插件 ID，用于标识请求者。<br/>Optional owner plugin id to identify the requester.</param>
/// <param name="DisplayName">可选的实例显示名称。<br/>Optional display name for the instance.</param>
/// <param name="UserDataDirectory">可选的用户数据目录路径。<br/>Optional user data directory path.</param>
/// <param name="PreviewInstanceId">可选的预览实例 ID，用于克隆或预览设置。<br/>Optional preview instance id to clone or preview settings from.</param>
public sealed record BrowserCreateInstanceRequest(string? OwnerPluginId, string? DisplayName, string? UserDataDirectory, string? PreviewInstanceId);

/// <summary>
/// 请求创建新标签页（可选地指定实例 Id 与导航 URL）。<br/>
/// Request to create a new tab (optionally specifying instance id and navigation URL).
/// </summary>
/// <param name="InstanceId">可选的目标实例 Id，若为空则使用当前实例。<br/>Optional target instance id; if null, the current instance is used.</param>
/// <param name="Url">要导航到的初始 URL（可选）。<br/>Optional initial URL to navigate the new tab to.</param>
public sealed record BrowserCreateTabRequest(string? InstanceId, string? Url);

/// <summary>
/// 创建实例响应，返回实例 Id 与可用的用户数据目录路径。<br/>
/// Response for instance creation containing the instance id and user data directory.
/// </summary>
/// <param name="InstanceId">新创建实例的 Id。<br/>Id of the newly created instance.</param>
/// <param name="UserDataDirectory">可用的用户数据目录路径（若适用）。<br/>User data directory path if applicable.</param>
public sealed record BrowserCreateInstanceResponse(string InstanceId, string? UserDataDirectory);

/// <summary>
/// 导航请求，包含目标 URL。<br/>
/// Navigation request containing the target URL.
/// </summary>
/// <param name="Url">要导航到的目标 URL。<br/>Target URL to navigate to.</param>
public sealed record BrowserNavigateRequest(string Url);

/// <summary>
/// 浏览器视口同步请求，包含宽度与高度。<br/>
/// Viewport sync request specifying width and height.
/// </summary>
/// <param name="Width">视口宽度（像素）。<br/>Viewport width in pixels.</param>
/// <param name="Height">视口高度（像素）。<br/>Viewport height in pixels.</param>
public sealed record BrowserViewportSyncRequest(int Width, int Height);

/// <summary>
/// 关闭标签页请求。<br/>
/// Request to close a tab.
/// </summary>
/// <param name="TabId">要关闭的标签页 Id。<br/>Id of the tab to close.</param>
public sealed record BrowserCloseTabRequest(string TabId);

/// <summary>
/// 关闭浏览器实例请求。<br/>
/// Request to close a browser instance.
/// </summary>
/// <param name="InstanceId">要关闭的浏览器实例 Id。<br/>Id of the browser instance to close.</param>
public sealed record BrowserCloseInstanceRequest(string InstanceId);

/// <summary>
/// 校验实例文件夹请求，包含根路径与文件夹名。<br/>
/// Request to validate an instance folder given a root path and folder name.
/// </summary>
/// <param name="RootPath">根路径，用于组合检查目标文件夹。<br/>Root path used to compose and validate the target folder.</param>
/// <param name="FolderName">要验证的文件夹名。<br/>Folder name to validate.</param>
public sealed record ValidateInstanceFolderRequest(string RootPath, string FolderName);

/// <summary>
/// 校验实例文件夹响应，包含是否有效与错误信息。<br/>
/// Response indicating whether the instance folder is valid, with optional error and resolved folder.
/// </summary>
/// <param name="Ok">标记验证是否通过。<br/>Indicates whether the validation passed.</param>
/// <param name="Error">若验证失败，包含错误信息。<br/>Error message if validation failed.</param>
/// <param name="Folder">解析后的文件夹路径（若成功）。<br/>Resolved folder path if validation succeeded.</param>
public sealed record ValidateInstanceFolderResponse(bool Ok, string? Error, string? Folder);

/// <summary>
/// 选择标签请求，仅包含标签 Id。<br/>
/// Request to select a tab by id.
/// </summary>
/// <param name="TabId">要选择的标签 Id。<br/>Id of the tab to select.</param>
public sealed record BrowserSelectTabRequest(string TabId);

/// <summary>
/// 投屏/屏幕共享设置请求，包含启用标志、最大宽高与帧间隔（毫秒）。<br/>
/// Screencast settings request including enabled flag, max dimensions and frame interval in ms.
/// </summary>
/// <param name="Enabled">是否启用投屏/屏幕共享。<br/>Whether screencast/screen sharing is enabled.</param>
/// <param name="MaxWidth">投屏最大宽度（像素）。<br/>Maximum screencast width in pixels.</param>
/// <param name="MaxHeight">投屏最大高度（像素）。<br/>Maximum screencast height in pixels.</param>
/// <param name="FrameIntervalMs">帧间隔（毫秒）。<br/>Frame interval in milliseconds.</param>
public sealed record ScreencastSettingsRequest(bool Enabled, int MaxWidth, int MaxHeight, int FrameIntervalMs);

/// <summary>
/// 布局相关设置请求（例如插件面板宽度）。<br/>
/// Layout related settings request (e.g., plugin panel width).
/// </summary>
/// <param name="PluginPanelWidth">插件面板宽度（像素）。<br/>Plugin panel width in pixels.</param>
public sealed record BrowserLayoutSettingsRequest(int PluginPanelWidth);

/// <summary>
/// 浏览器状态响应，包含当前 URL、标题、投屏设置、资源使用情况、页签信息及当前实例等信息。<br/>
/// Browser status response containing current URL, title, screencast settings, resource usage, tabs and current instance info.
/// </summary>
/// <param name="CurrentUrl">当前页面的 URL（可空）。<br/>Current page URL, nullable.</param>
/// <param name="Title">当前页面标题（可空）。<br/>Current page title, nullable.</param>
/// <param name="ErrorMessage">可选的错误信息（若有）。<br/>Optional error message if present.</param>
/// <param name="SupportsScreencast">宿主是否支持投屏。<br/>Whether the host supports screencast.</param>
/// <param name="ScreencastEnabled">当前是否启用了投屏。<br/>Whether screencast is currently enabled.</param>
/// <param name="ScreencastMaxWidth">投屏最大宽度。<br/>Maximum screencast width.</param>
/// <param name="ScreencastMaxHeight">投屏最大高度。<br/>Maximum screencast height.</param>
/// <param name="ScreencastFrameIntervalMs">投屏帧间隔（毫秒）。<br/>Screencast frame interval in milliseconds.</param>
/// <param name="CpuUsagePercent">CPU 使用率百分比。<br/>CPU usage percentage.</param>
/// <param name="MemoryUsageMb">内存使用量（MB）。<br/>Memory usage in MB.</param>
/// <param name="TotalPageCount">总页签数量。<br/>Total number of pages/tabs.</param>
/// <param name="PluginPanelWidth">插件面板宽度（像素）。<br/>Plugin panel width in pixels.</param>
/// <param name="Tabs">当前页签信息列表。<br/>List of current tab information.</param>
/// <param name="CurrentInstanceId">当前实例 Id（可空）。<br/>Current instance id, nullable.</param>
/// <param name="CurrentViewport">当前视口设置响应 DTO。<br/>Current viewport settings response DTO.</param>
/// <param name="IsHeadless">是否无头模式。<br/>Whether the browser is running headless.</param>
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
/// 单个页签信息 DTO。<br/>
/// DTO for a single browser tab info.
/// </summary>
/// <param name="Id">页签 Id。<br/>Tab id.</param>
/// <param name="Title">页签标题（可空）。<br/>Tab title, nullable.</param>
/// <param name="Url">页签当前 URL（可空）。<br/>Tab current URL, nullable.</param>
/// <param name="IsActive">是否为活动标签。<br/>Whether the tab is active.</param>
/// <param name="OwnerPluginId">拥有者插件 Id（可空）。<br/>Owner plugin id, nullable.</param>
public sealed record BrowserTabInfoDto(string Id, string? Title, string? Url, bool IsActive, string? OwnerPluginId = null);

/// <summary>
/// 浏览器视口设置响应 DTO（宽度、高度与类型）。<br/>
/// Viewport settings response DTO (width, height and type).
/// </summary>
/// <param name="Width">视口宽度（像素）。<br/>Viewport width in pixels.</param>
/// <param name="Height">视口高度（像素）。<br/>Viewport height in pixels.</param>
/// <param name="ViewportType">视口类型（例如 Auto/Fixed）。<br/>Viewport type, e.g. Auto or Fixed.</param>
public sealed record BrowserInstanceViewportSettingsResponseDto(int Width, int Height, string ViewportType = "Auto");

