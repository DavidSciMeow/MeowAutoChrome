using Microsoft.Playwright;

namespace MeowAutoChrome.Core.Interface;

/// <summary>
/// Core 层的浏览器实例管理器抽象，负责管理多个浏览器实例。<br/>
/// Core-internal browser instance manager abstraction responsible for managing multiple browser instances.
/// </summary>
public interface ICoreBrowserInstanceManager
{
    /// <summary>
    /// 当前管理的实例集合。<br/>
    /// Read-only collection of managed instances.
    /// </summary>
    IReadOnlyCollection<ICoreBrowserInstance> Instances { get; }
    /// <summary>
    /// 当前活跃实例 ID。<br/>
    /// Current active instance id.
    /// </summary>
    string CurrentInstanceId { get; }
    /// <summary>
    /// 是否为无头模式。<br/>
    /// Whether instances are running in headless mode.
    /// </summary>
    bool IsHeadless { get; }

    /// <summary>
    /// 当前的浏览器上下文（可能为 null）。<br/>
    /// Current browser context (may be null).
    /// </summary>
    IBrowserContext? BrowserContext { get; }
    /// <summary>
    /// 当前活动页面（可能为 null）。<br/>
    /// Current active page (may be null).
    /// </summary>
    IPage? ActivePage { get; }
    /// <summary>
    /// 当前选中页面 ID（可空）。<br/>
    /// Currently selected page id (nullable).
    /// </summary>
    string? SelectedPageId { get; }

    /// <summary>
    /// 尝试根据 ID 获取实例。<br/>
    /// Try to get an instance by id.
    /// </summary>
    /// <param name="id">实例 ID / instance id to find.</param>
    /// <param name="inst">当返回 true 时输出找到的实例 / out parameter containing the found instance when true.</param>
    bool TryGet(string id, out ICoreBrowserInstance inst);
    /// <summary>
    /// 创建新实例并返回其实例 ID。<br/>
    /// Create a new instance and return its id.
    /// </summary>
    /// <param name="ownerId">实例所属者标识（例如插件 Id）/ owner identifier (e.g., plugin id).</param>
    /// <param name="displayName">实例显示名称 / display name for the instance.</param>
    /// <param name="userDataDir">用户数据目录路径 / user data directory path.</param>
    /// <param name="headless">是否以无头模式运行 / whether to run headless.</param>
    /// <param name="previewInstanceId">可选的预览实例 ID / optional preview instance id.</param>
    Task<string> CreateAsync(string ownerId, string displayName, string userDataDir, bool headless = true, string? previewInstanceId = null);
    /// <summary>
    /// 预览新实例的创建（返回实例 ID 与用户数据目录）。<br/>
    /// Preview the creation of a new instance (returns instance id and user data directory).
    /// </summary>
    /// <param name="ownerId">实例所属者标识 / owner identifier.</param>
    /// <param name="userDataDirRoot">可选的用户数据目录根路径 / optional user data directory root.</param>
    Task<(string InstanceId, string UserDataDirectory)> PreviewNewInstanceAsync(string ownerId, string? userDataDirRoot = null);
    /// <summary>
    /// 获取由某插件创建的实例 ID 列表。<br/>
    /// Get the list of instance ids created by a plugin.
    /// </summary>
    /// <param name="pluginId">插件 ID / plugin id whose created instances to list.</param>
    IReadOnlyList<string> GetPluginInstanceIds(string pluginId);
    /// <summary>
    /// 根据实例 ID 获取实例（可能为 null）。<br/>
    /// Get an instance by id, or null if not found.
    /// </summary>
    /// <param name="instanceId">目标实例 ID / target instance id.</param>
    ICoreBrowserInstance? GetInstance(string instanceId);
    /// <summary>
    /// 关闭指定实例。<br/>
    /// Close the specified instance.
    /// </summary>
    /// <param name="instanceId">要关闭的实例 ID / instance id to close.</param>
    Task<bool> CloseInstanceAsync(string instanceId);

    /// <summary>
    /// 将指定实例切换为当前实例。<br/>
    /// Switch the specified instance to become the current instance.
    /// </summary>
    /// <param name="instanceId">要切换到的实例 ID / instance id to switch to.</param>
    /// <param name="cancellationToken">可选取消令牌 / optional cancellation token.</param>
    Task<bool> SelectBrowserInstanceAsync(string instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新启动配置并可强制重载。<br/>
    /// Update launch settings and optionally force reload.
    /// </summary>
    /// <param name="primaryUserDataDirectory">主用户数据目录 / primary user data directory.</param>
    /// <param name="isHeadless">是否无头运行 / whether headless mode is enabled.</param>
    /// <param name="forceReload">是否强制重载实例 / whether to force reload instances.</param>
    Task UpdateLaunchSettingsAsync(string primaryUserDataDirectory, bool isHeadless, bool forceReload = false);
}
