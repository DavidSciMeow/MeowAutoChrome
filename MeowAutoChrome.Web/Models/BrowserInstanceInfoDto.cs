namespace MeowAutoChrome.Web.Models;

/// <summary>
/// 表示浏览器实例信息的 DTO（供 Web 层使用），避免直接引用 Core 层类型。<br/>
/// DTO representing browser instance information for the Web layer (avoids referencing Core model types).
/// </summary>
/// <param name="Id">实例 Id。<br/>Instance id.</param>
/// <param name="DisplayName">实例显示名称。<br/>Display name of the instance.</param>
/// <param name="UserDataDirectory">可选的用户数据目录路径。<br/>Optional user data directory path.</param>
/// <param name="Color">用于 UI 的颜色字符串。<br/>Color string used for UI.</param>
/// <param name="IsCurrent">是否为当前实例。<br/>Whether this is the current instance.</param>
/// <param name="PageCount">该实例的页签数量。<br/>Number of pages/tabs in the instance.</param>
public record BrowserInstanceInfoDto(
    string Id,
    string DisplayName,
    string? UserDataDirectory,
    string Color,
    bool IsCurrent,
    int PageCount
);
