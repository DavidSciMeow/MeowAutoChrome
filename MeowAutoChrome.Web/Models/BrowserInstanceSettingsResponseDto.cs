namespace MeowAutoChrome.Web.Models;

/// <summary>
/// 浏览器实例设置响应 DTO，包含实例 Id、显示名与可选的用户数据目录。<br/>
/// Browser instance settings response DTO containing instance id, display name and optional user data directory.
/// </summary>
/// <param name="InstanceId">实例 Id。<br/>Instance id.</param>
/// <param name="DisplayName">实例显示名称。<br/>Display name.</param>
/// <param name="UserDataDirectory">可选的用户数据目录路径。<br/>Optional user data directory path.</param>
public sealed record BrowserInstanceSettingsResponseDto(string InstanceId, string DisplayName, string? UserDataDirectory = null);
