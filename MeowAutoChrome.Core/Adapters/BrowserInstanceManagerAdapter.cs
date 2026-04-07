using MeowAutoChrome.Core.Models;
using MeowAutoChrome.Core.Interface;
using Microsoft.Playwright;

namespace MeowAutoChrome.Core.Adapters;

/// <summary>
/// 将核心的 `ICoreBrowserInstanceManager` 适配为较窄/兼容的外部接口包装器。
/// 用于将核心实现暴露给依赖于更高层 API 的组件（不支持所有写操作）。<br/>
/// Adapter that exposes the core `ICoreBrowserInstanceManager` as a narrower/compatible wrapper for higher-level components. Not all write operations are supported.
/// </summary>
/// <remarks>
/// 创建适配器并注入核心管理器实现。<br/>
/// Create the adapter and inject the core manager implementation.
/// </remarks>
/// <param name="core">核心浏览器实例管理器 / core browser instance manager.</param>
public sealed class BrowserInstanceManagerAdapter(ICoreBrowserInstanceManager core)
{
    private readonly ICoreBrowserInstanceManager _core = core ?? throw new ArgumentNullException(nameof(core));

    // Read-only query functions can be mapped
    /// <summary>
    /// 当前实例 Id 的简易访问器（委托到核心管理器）。<br/>
    /// Simple accessor for the current instance id delegating to the core manager.
    /// </summary>
    public string CurrentInstanceId => _core.CurrentInstanceId;

    /// <summary>
    /// 通过适配器请求创建新标签页（适配器不支持写操作）。<br/>
    /// Request to create a new tab via the adapter (write operations are not supported by this adapter).
    /// </summary>
    /// <param name="url">可选要打开的 URL / optional URL to open.</param>
    /// <returns>表示操作的任务 / A Task representing the operation.</returns>
    public Task CreateTabAsync(string? url = null) => throw new NotSupportedException("CreateTabAsync must be performed via Core APIs.");

    /// <summary>
    /// 获取当前页面标题（对于此适配器通常不可用，返回 null）。<br/>
    /// Get the current page title (usually unavailable for this adapter; returns null).
    /// </summary>
    /// <returns>页面标题或 null / Page title or null.</returns>
    public Task<string?> GetTitleAsync() => Task.FromResult<string?>(null);

    /// <summary>
    /// 请求导航到指定 URL（适配器不支持写操作）。<br/>
    /// Request navigation to the specified URL (write operations not supported by this adapter).
    /// </summary>
    /// <param name="url">目标 URL / target URL.</param>
    /// <returns>表示操作的任务 / A Task representing the operation.</returns>
    public Task NavigateAsync(string url) => throw new NotSupportedException();

    /// <summary>
    /// 后退导航（不支持）。<br/>
    /// Back navigation (not supported).
    /// </summary>
    /// <returns>表示操作的任务 / A Task representing the operation.</returns>
    public Task GoBackAsync() => throw new NotSupportedException();

    /// <summary>
    /// 前进导航（不支持）。<br/>
    /// Forward navigation (not supported).
    /// </summary>
    /// <returns>表示操作的任务 / A Task representing the operation.</returns>
    public Task GoForwardAsync() => throw new NotSupportedException();

    /// <summary>
    /// 重新加载当前页面（不支持）。<br/>
    /// Reload the current page (not supported).
    /// </summary>
    /// <returns>表示操作的任务 / A Task representing the operation.</returns>
    public Task ReloadAsync() => throw new NotSupportedException();

    /// <summary>
    /// 捕获屏幕截图（适配器未实现，返回 null）。<br/>
    /// Capture a screenshot (adapter not implemented; returns null).
    /// </summary>
    /// <returns>字节数组或 null / byte[] or null.</returns>
    public Task<byte[]?> CaptureScreenshotAsync() => Task.FromResult<byte[]?>(null);

    /// <summary>
    /// 设置视口大小（不支持）。<br/>
    /// Set viewport size (not supported).
    /// </summary>
    /// <param name="width">宽度 / width.</param>
    /// <param name="height">高度 / height.</param>
    /// <returns>表示操作的任务 / A Task representing the operation.</returns>
    public Task SetViewportSizeAsync(int width, int height) => throw new NotSupportedException();

    /// <summary>
    /// 更新启动设置（不支持）。<br/>
    /// Update launch settings (not supported).
    /// </summary>
    /// <param name="primaryUserDataDirectory">主用户数据目录 / primary user data directory.</param>
    /// <param name="isHeadless">是否无头 / whether to run headless.</param>
    /// <param name="forceReload">是否强制重载 / force reload flag.</param>
    /// <returns>表示操作的任务 / A Task representing the operation.</returns>
    public Task UpdateLaunchSettingsAsync(string primaryUserDataDirectory, bool isHeadless, bool forceReload = false) => throw new NotSupportedException();
    /// <summary>
    /// 更新指定实例的设置（适配器不支持写操作）。<br/>
    /// Update settings for the specified instance (adapter does not support write operations).
    /// </summary>
    /// <param name="request">包含更新信息的请求 / request with update information.</param>
    /// <param name="cancellationToken">取消令牌 / cancellation token.</param>
    /// <returns>表示操作的任务 / A Task representing the operation.</returns>
    public Task<bool> UpdateInstanceSettingsAsync(BrowserInstanceSettingsUpdateRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <summary>
    /// 同步当前实例的视口尺寸（适配器不支持写操作）。<br/>
    /// Sync the current instance viewport size (adapter does not support write operations).
    /// </summary>
    /// <param name="width">宽度 / width.</param>
    /// <param name="height">高度 / height.</param>
    /// <param name="cancellationToken">取消令牌 / cancellation token.</param>
    /// <returns>表示操作的任务 / A Task representing the operation.</returns>
    public Task SyncCurrentInstanceViewportAsync(int width, int height, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <summary>
    /// 关闭指定标签页（适配器不支持写操作）。<br/>
    /// Close the specified tab (adapter does not support write operations).
    /// </summary>
    /// <param name="tabId">标签页 Id / tab id.</param>
    /// <returns>表示操作的任务 / A Task representing the operation.</returns>
    public Task<bool> CloseTabAsync(string tabId) => throw new NotSupportedException();

    /// <summary>
    /// 关闭指定浏览器实例（适配器不支持写操作）。<br/>
    /// Close the specified browser instance (adapter does not support write operations).
    /// </summary>
    /// <param name="instanceId">实例 Id / instance id.</param>
    /// <param name="cancellationToken">取消令牌 / cancellation token.</param>
    /// <returns>表示操作的任务 / A Task representing the operation.</returns>
    public Task<bool> CloseBrowserInstanceAsync(string instanceId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <summary>
    /// 选择指定标签页（适配器不支持写操作）。<br/>
    /// Select the specified tab (adapter does not support write operations).
    /// </summary>
    /// <param name="tabId">标签页 Id / tab id.</param>
    /// <returns>表示操作的任务 / A Task representing the operation.</returns>
    public Task<bool> SelectPageAsync(string tabId) => throw new NotSupportedException();

    /// <summary>
    /// 创建浏览器实例（适配器不支持写操作）。<br/>
    /// Create a browser instance (adapter does not support write operations).
    /// </summary>
    /// <param name="ownerPluginId">拥有者插件 Id / owner plugin id.</param>
    /// <param name="displayName">可选显示名称 / optional display name.</param>
    /// <param name="userDataDirectory">可选用户数据目录 / optional user data directory.</param>
    /// <param name="previewInstanceId">可选预览实例 Id / optional preview instance id.</param>
    /// <param name="cancellationToken">取消令牌 / cancellation token.</param>
    /// <returns>表示操作的任务，返回创建的实例 Id / A Task returning the created instance id.</returns>
    public Task<string> CreateBrowserInstanceAsync(string ownerPluginId, string? displayName = null, string? userDataDirectory = null, string? previewInstanceId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <summary>
    /// 移除浏览器实例（适配器不支持写操作）。<br/>
    /// Remove a browser instance (adapter does not support write operations).
    /// </summary>
    /// <param name="instanceId">实例 Id / instance id.</param>
    /// <param name="cancellationToken">取消令牌 / cancellation token.</param>
    /// <returns>表示操作的任务 / A Task representing the operation.</returns>
    public Task<bool> RemoveBrowserInstanceAsync(string instanceId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <summary>
    /// 选择浏览器实例（适配器不支持写操作）。<br/>
    /// Select a browser instance (adapter does not support write operations).
    /// </summary>
    /// <param name="instanceId">实例 Id / instance id.</param>
    /// <param name="cancellationToken">取消令牌 / cancellation token.</param>
    /// <returns>表示操作的任务 / A Task representing the operation.</returns>
    public Task<bool> SelectBrowserInstanceAsync(string instanceId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <summary>
    /// 预览新实例（适配器不支持写操作）。<br/>
    /// Preview a new instance (adapter does not support write operations).
    /// </summary>
    /// <param name="ownerPluginId">可选的拥有者插件 Id / optional owner plugin id.</param>
    /// <param name="userDataDirectoryRoot">可选的用户数据目录根路径 / optional user data directory root.</param>
    /// <returns>包含预览实例 Id 与用户数据目录的任务 / A Task returning the preview instance id and user data directory.</returns>
    public Task<(string InstanceId, string UserDataDirectory)> PreviewNewInstanceAsync(string? ownerPluginId, string? userDataDirectoryRoot) => throw new NotSupportedException();

    // IBrowserInstanceQuery members
    /// <summary>
    /// 获取当前管理的实例列表（适配器未实现）。<br/>
    /// Get the list of currently managed instances (not implemented by adapter).
    /// </summary>
    /// <returns>实例信息列表 / list of instance info.</returns>
    public IReadOnlyList<BrowserInstanceInfo> GetInstances() => throw new NotSupportedException();

    /// <summary>
    /// 获取指定插件拥有的实例 Id 列表（适配器未实现）。<br/>
    /// Get the list of instance ids owned by the specified plugin (not implemented by adapter).
    /// </summary>
    /// <param name="pluginId">插件 Id / plugin id.</param>
    /// <returns>实例 Id 列表 / list of instance ids.</returns>
    public IReadOnlyList<string> GetPluginInstanceIds(string pluginId) => throw new NotSupportedException();

    /// <summary>
    /// 获取实例显示颜色（适配器返回 null 或占位）。<br/>
    /// Get the instance display color (adapter returns null or placeholder).
    /// </summary>
    /// <param name="instanceId">实例 Id / instance id.</param>
    /// <returns>颜色字符串或 null / color string or null.</returns>
    public string? GetInstanceColor(string instanceId) => null;

    /// <summary>
    /// 获取指定实例的浏览器上下文（委托到核心）。<br/>
    /// Get the browser context for the specified instance (delegates to core).
    /// </summary>
    /// <param name="instanceId">实例 Id / instance id.</param>
    /// <returns>Playwright 浏览器上下文或 null / Playwright browser context or null.</returns>
    public IBrowserContext? GetBrowserContext(string instanceId) => _core.BrowserContext;

    /// <summary>
    /// 获取指定实例的活动页面（委托到核心）。<br/>
    /// Get the active page for the specified instance (delegates to core).
    /// </summary>
    /// <param name="instanceId">实例 Id / instance id.</param>
    /// <returns>活动页面或 null / active page or null.</returns>
    public IPage? GetActivePage(string instanceId) => _core.ActivePage;

    /// <summary>
    /// 枚举标签页信息（适配器未实现）。<br/>
    /// Enumerate tab infos (adapter not implemented).
    /// </summary>
    /// <returns>标签页摘要列表 / list of tab summaries.</returns>
    public Task<IReadOnlyList<BrowserTabInfo>> GetTabsAsync() => throw new NotSupportedException();

    /// <summary>
    /// 获取当前实例的视口设置（适配器未实现）。<br/>
    /// Get current instance viewport settings (adapter not implemented).
    /// </summary>
    /// <returns>视口设置响应对象 / viewport settings response object.</returns>
    public BrowserInstanceViewportSettingsResponse GetCurrentInstanceViewportSettings() => throw new NotSupportedException();

    /// <summary>
    /// 当前 URL（适配器未实现）。<br/>
    /// Current URL (adapter not implemented).
    /// </summary>
    public string? CurrentUrl => null;

    /// <summary>
    /// 管理器内页面总数（适配器未实现）。<br/>
    /// Total page count in manager (adapter not implemented).
    /// </summary>
    public int TotalPageCount => 0;

    /// <summary>
    /// 获取指定实例的设置响应（适配器未实现）。<br/>
    /// Get instance settings response for the specified instance (adapter not implemented).
    /// </summary>
    /// <param name="instanceId">实例 Id / instance id.</param>
    /// <returns>实例设置或 null / instance settings or null.</returns>
    public Task<BrowserInstanceSettingsResponse?> GetInstanceSettingsAsync(string instanceId) => throw new NotSupportedException();
}
