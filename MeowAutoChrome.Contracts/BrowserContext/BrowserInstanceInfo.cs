namespace MeowAutoChrome.Contracts.BrowserContext;

/// <summary>
/// 浏览器实例信息，用于描述和管理浏览器实例的基本信息
/// </summary>
/// <param name="Id">实例ID</param>
/// <param name="Name">实例名称</param>
/// <param name="OwnerPluginId">所属插件ID</param>
/// <param name="Color">实例颜色</param>
/// <param name="IsSelected">是否选中</param>
/// <param name="PageCount">页面数量</param>
public sealed record BrowserInstanceInfo(string Id, string Name, string? OwnerPluginId, string Color, bool IsSelected, int PageCount);
