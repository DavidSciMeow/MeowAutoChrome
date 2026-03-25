using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MeowAutoChrome.Contracts.BrowserContext;
using Microsoft.Playwright;

namespace MeowAutoChrome.Contracts.Interface;

/// <summary>
/// 浏览器实例管理器接口，提供对浏览器实例的管理和操作能力，包括获取实例信息、创建和删除实例、选择实例等功能，插件可以通过这个接口与宿主进行交互，获取当前浏览器实例的信息或执行跨实例的操作
/// </summary>
public interface IBrowserInstanceManager
{
    /// <summary>
    /// 当前浏览器实例ID，唯一标识当前插件所在的浏览器实例，插件可以通过这个ID与宿主进行交互或区分不同的浏览器实例
    /// </summary>
    // CurrentInstanceId already defined earlier in concrete implementations; keep in interface but avoid duplicate declarations.
    string CurrentInstanceId { get; }
    /// <summary>
    /// 获取所有浏览器实例的信息，返回一个只读列表，包含每个实例的ID、名称、所属插件ID、颜色、是否选中和页面数量等基本信息，插件可以通过这些信息了解当前浏览器实例的状态和分布情况，并进行相应的操作或展示
    /// </summary>
    /// <returns></returns>
    IReadOnlyList<BrowserInstanceInfo> GetInstances();
    /// <summary>
    /// 获取指定插件ID的浏览器实例ID列表，返回一个只读列表，包含所有属于该插件的浏览器实例的ID，插件可以通过这个方法获取自己创建的浏览器实例的ID，以便进行后续的操作或管理，如果没有找到任何实例，则返回一个空列表
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    /// <returns></returns>
    IReadOnlyList<string> GetPluginInstanceIds(string pluginId);
    /// <summary>
    /// 获取指定浏览器实例ID的颜色，返回一个字符串，表示该实例的颜色，插件可以通过这个方法获取某个浏览器实例的颜色，以便在UI上进行区分或展示，如果没有找到该实例，则返回null
    /// </summary>
    /// <param name="instanceId">浏览器实例ID</param>
    /// <returns></returns>
    string? GetInstanceColor(string instanceId);
    /// <summary>
    /// 获取指定浏览器实例ID的浏览器上下文，返回一个IBrowserContext对象，表示该实例的浏览器上下文，插件可以通过这个方法获取某个浏览器实例的上下文，以便在该实例中执行浏览器相关的操作或获取相关的信息，如果没有找到该实例，则返回null
    /// </summary>
    /// <param name="instanceId"></param>
    /// <returns></returns>
    IBrowserContext? GetBrowserContext(string instanceId);
    /// <summary>
    /// 获取指定浏览器实例ID的活动页面，返回一个IPage对象，表示该实例的活动页面，插件可以通过这个方法获取某个浏览器实例的活动页面，以便在该页面中执行浏览器相关的操作或获取相关的信息，如果没有找到该实例或该实例没有活动页面，则返回null
    /// </summary>
    /// <param name="instanceId">浏览器实例ID</param>
    /// <returns></returns>
    IPage? GetActivePage(string instanceId);
    /// <summary>
    /// 获取当前所有标签页的列表（用于前端展示）。
    /// </summary>
    Task<IReadOnlyList<BrowserTabInfo>> GetTabsAsync();

    /// <summary>
    /// 获取当前实例视口设置（宽高与自动调整选项）。
    /// </summary>
    BrowserInstanceViewportSettingsResponse GetCurrentInstanceViewportSettings();
    /// <summary>
    /// 当前实例的 URL（若存在活动页）。
    /// </summary>
    string? CurrentUrl { get; }

    /// <summary>
    /// 当前总页面数。
    /// </summary>
    int TotalPageCount { get; }

    Task CreateTabAsync(string? url = null);

    Task<string?> GetTitleAsync();

    Task NavigateAsync(string url);

    Task GoBackAsync();

    Task GoForwardAsync();

    Task ReloadAsync();

    Task<byte[]?> CaptureScreenshotAsync();

    Task SetViewportSizeAsync(int width, int height);

    Task UpdateLaunchSettingsAsync(string primaryUserDataDirectory, bool isHeadless, bool forceReload = false);
    /// <summary>
    /// 获取指定实例的设置。
    /// </summary>
    Task<MeowAutoChrome.Contracts.BrowserContext.BrowserInstanceSettingsResponse?> GetInstanceSettingsAsync(string instanceId);

    /// <summary>
    /// 更新实例设置。
    /// </summary>
    Task<bool> UpdateInstanceSettingsAsync(string instanceId, string userDataDirectory, int viewportWidth, int viewportHeight, bool autoResizeViewport, bool preserveAspectRatio, bool useProgramUserAgent, string? userAgent, bool migrateExistingUserData, int? displayWidth = null, int? displayHeight = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 同步当前实例视口大小。
    /// </summary>
    Task SyncCurrentInstanceViewportAsync(int width, int height, CancellationToken cancellationToken = default);

    /// <summary>
    /// 关闭指定标签页。
    /// </summary>
    Task<bool> CloseTabAsync(string tabId);

    /// <summary>
    /// 关闭并移除指定浏览器实例。
    /// </summary>
    Task<bool> CloseBrowserInstanceAsync(string instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 选择指定标签页为活动页。
    /// </summary>
    Task<bool> SelectPageAsync(string tabId);
    /// <summary>
    /// 创建一个新的浏览器实例，返回新实例的ID，插件可以通过这个方法创建一个新的浏览器实例，并指定所属插件ID、显示名称、用户数据目录等参数，以便在该实例中执行浏览器相关的操作或获取相关的信息，如果创建失败，则抛出异常或返回null
    /// </summary>
    /// <param name="ownerPluginId">插件ID</param>
    /// <param name="displayName">显示名称</param>
    /// <param name="userDataDirectory">用户数据目录</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    Task<string> CreateBrowserInstanceAsync(string ownerPluginId, string? displayName = null, string? userDataDirectory = null, string? previewInstanceId = null, CancellationToken cancellationToken = default);
    /// <summary>
    /// 删除指定浏览器实例，返回一个布尔值，表示删除是否成功，插件可以通过这个方法删除一个浏览器实例，并指定实例ID，如果删除成功，则返回true，否则返回false，如果没有找到该实例，则返回false
    /// </summary>
    /// <param name="instanceId">浏览器实例ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    Task<bool> RemoveBrowserInstanceAsync(string instanceId, CancellationToken cancellationToken = default);
    /// <summary>
    /// 选择指定浏览器实例，返回一个布尔值，表示选择是否成功，插件可以通过这个方法选择一个浏览器实例，并指定实例ID，如果选择成功，则返回true，否则返回false，如果没有找到该实例，则返回false，选择一个实例后，当前插件的浏览器上下文和活动页面将切换到该实例，以便在该实例中执行浏览器相关的操作或获取相关的信息
    /// </summary>
    /// <param name="instanceId">浏览器实例ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    Task<bool> SelectBrowserInstanceAsync(string instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 预览将要创建的实例 ID 与 user-data 目录（不实际创建）。
    /// </summary>
    Task<(string InstanceId, string UserDataDirectory)> PreviewNewInstanceAsync(string? ownerPluginId, string? userDataDirectoryRoot);
}
