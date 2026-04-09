namespace MeowAutoChrome.Core.Models;

/// <summary>
/// 浏览器实例信息（用于 UI 展示）。<br/>
/// Browser instance information for UI display.
/// </summary>
/// <param name="Id">实例 Id / instance id.</param>
/// <param name="Name">显示名称 / display name.</param>
/// <param name="OwnerPluginId">拥有者插件 Id（可空）/ owner plugin id (nullable).</param>
/// <param name="Color">用于显示的颜色字符串 / color string for UI display.</param>
/// <param name="IsSelected">是否被选中 / whether selected.</param>
/// <param name="PageCount">页面数量 / number of pages.</param>
public sealed record BrowserInstanceInfo(string Id, string Name, string? OwnerPluginId, string Color, bool IsSelected, int PageCount);
/// <summary>
/// 浏览器页签信息。<br/>
/// Browser tab information.
/// </summary>
/// <param name="Id">页签 Id / tab id.</param>
/// <param name="Title">页签标题 / tab title.</param>
/// <param name="Url">页签 URL / tab url.</param>
/// <param name="IsActive">是否处于激活状态 / whether active.</param>
/// <param name="OwnerPluginId">拥有者插件 Id（可空）/ owner plugin id (nullable).</param>
public sealed record BrowserTabInfo(string Id, string Title, string Url, bool IsActive, string? OwnerPluginId = null);
/// <summary>
/// 视口设置响应（宽、高与类型）。<br/>
/// Viewport settings response (width, height and type).
/// </summary>
/// <param name="Width">视口宽度（像素）/ viewport width in pixels.</param>
/// <param name="Height">视口高度（像素）/ viewport height in pixels.</param>
/// <param name="ViewportType">视口类型（如 Auto/Custom）/ viewport type (e.g., Auto/Custom).</param>
public sealed record BrowserInstanceViewportSettingsResponse(int Width, int Height, string ViewportType = "Auto");
/// <summary>
/// 浏览器实例设置响应（实例 ID、显示名与用户数据目录）。<br/>
/// Browser instance settings response (instance id, display name, user data directory).
/// </summary>
/// <param name="InstanceId">实例 ID / instance id.</param>
/// <param name="DisplayName">实例显示名 / instance display name.</param>
/// <param name="UserDataDirectory">用户数据目录（可空）/ user data directory (nullable).</param>
public sealed record BrowserInstanceSettingsResponse(string InstanceId, string DisplayName, string? UserDataDirectory = null);
/// <summary>
/// 浏览器实例设置更新请求载荷。<br/>
/// Payload for updating browser instance settings.
/// </summary>
/// <param name="InstanceId">目标实例 ID / target instance id.</param>
/// <param name="IsHeadless">是否无头运行 / whether headless mode.</param>
/// <param name="UserDataDirectory">用户数据目录（可空）/ user data directory (nullable).</param>
/// <param name="ViewportWidth">视口宽度 / viewport width.</param>
/// <param name="ViewportHeight">视口高度 / viewport height.</param>
public sealed record BrowserInstanceSettingsUpdateRequest(string InstanceId, bool IsHeadless, string? UserDataDirectory, int ViewportWidth, int ViewportHeight);
