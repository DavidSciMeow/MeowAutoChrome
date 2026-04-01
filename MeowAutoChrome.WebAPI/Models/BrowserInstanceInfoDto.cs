namespace MeowAutoChrome.WebAPI.Models;

/// <summary>
/// 浏览器实例信息 DTO。<br/>
/// Browser instance information DTO.
/// </summary>
/// <param name="Id">实例 ID。<br/>Instance id.</param>
/// <param name="DisplayName">显示名称。<br/>Display name.</param>
/// <param name="UserDataDirectory">用户数据目录。<br/>User data directory.</param>
/// <param name="Color">界面显示颜色。<br/>UI display color.</param>
/// <param name="IsCurrent">是否当前选中。<br/>Whether the instance is currently selected.</param>
/// <param name="PageCount">标签页数量。<br/>Tab count.</param>
public record BrowserInstanceInfoDto(
    string Id,
    string DisplayName,
    string? UserDataDirectory,
    string Color,
    bool IsCurrent,
    int PageCount
);
