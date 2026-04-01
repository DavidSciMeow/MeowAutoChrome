using Microsoft.Playwright;
using MeowAutoChrome.Core.Struct;
using MeowAutoChrome.Core.Interface;
using MeowAutoChrome.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Concurrent;

namespace MeowAutoChrome.Core;

/// <summary>
/// 浏览器实例管理核心，负责创建和管理 Playwright 实例、标签页以及对外的查询/控制 API。<br/>
/// Core manager for browser instances responsible for creating and managing Playwright instances, tabs, and providing query/control APIs.
/// </summary>
public class BrowserInstanceManagerCore : ICoreBrowserInstanceManager
{
    private readonly ILogger<BrowserInstanceManagerCore> _logger;
    private readonly ILoggerFactory? _loggerFactory;
    private readonly IProgramSettingsProvider? _settingsProvider;
    private readonly ConcurrentDictionary<string, ICoreBrowserInstance> _instances = new ConcurrentDictionary<string, ICoreBrowserInstance>();
    private string? _currentInstanceId;

    /// <summary>
    /// 构造函数，注入日志、日志工厂与可选的设置提供者。<br/>
    /// Constructor that injects logger, optional logger factory and an optional settings provider.
    /// </summary>
    /// <param name="logger">记录器实例 / logger.</param>
    /// <param name="loggerFactory">可选的日志工厂 / optional logger factory.</param>
    /// <param name="settingsProvider">可选的程序设置提供者 / optional program settings provider.</param>
    public BrowserInstanceManagerCore(ILogger<BrowserInstanceManagerCore> logger, ILoggerFactory? loggerFactory = null, IProgramSettingsProvider? settingsProvider = null)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _settingsProvider = settingsProvider;
    }

    // Note: this core class does not implement IBrowserInstanceManager directly.
    // A host-level adapter implements IBrowserInstanceManager and delegates
    // to this core class.

    // PreviewNewInstanceAsync is provided as a public method for wrappers to call.

    /// <summary>
    /// Preview a new instance id and the user-data directory that would be used if created.
    /// This does not create any files or instances.
    /// </summary>
    public async Task<(string InstanceId, string UserDataDirectory)> PreviewNewInstanceAsync(string ownerId, string? userDataDirRoot = null)
    {
        var settings = _settingsProvider is null ? new ProgramSettings() : await _settingsProvider.GetAsync();
        var root = string.IsNullOrWhiteSpace(userDataDirRoot) ? settings.UserDataDirectory : userDataDirRoot!;
        var shortId = Guid.NewGuid().ToString("N").Substring(0, 8);
        var id = $"{ownerId}-{shortId}";
        var instanceUserDataDir = Path.Combine(root, id);
        return (id, instanceUserDataDir);
    }

    /// <summary>
    /// 当前管理的所有核心浏览器实例的只读集合。<br/>
    /// Read-only collection of all managed core browser instances.
    /// </summary>
    public IReadOnlyCollection<ICoreBrowserInstance> Instances => _instances.Values.ToList().AsReadOnly();

    /// <summary>
    /// 当前所选或活动的浏览器实例（如果存在）。<br/>
    /// The currently selected or active browser instance, if any.
    /// </summary>
    public ICoreBrowserInstance? CurrentInstance
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(_currentInstanceId) && _instances.TryGetValue(_currentInstanceId, out var inst))
                return inst;
            return _instances.Values.FirstOrDefault();
        }
    }

    /// <summary>
    /// 当前实例的标识符字符串（若无则返回空字符串）。<br/>
    /// Identifier of the current instance or empty string if none.
    /// </summary>
    public string CurrentInstanceId => !string.IsNullOrWhiteSpace(_currentInstanceId) ? _currentInstanceId : (CurrentInstance?.InstanceId ?? string.Empty);

    /// <summary>
    /// 指示当前实例是否以无头模式运行。<br/>
    /// Indicates whether the current instance is running headless.
    /// </summary>
    public bool IsHeadless => CurrentInstance?.IsHeadless ?? true;

    IBrowserContext? ICoreBrowserInstanceManager.BrowserContext => CurrentInstance?.BrowserContext;
    IPage? ICoreBrowserInstanceManager.ActivePage => CurrentInstance?.GetSelectedPage();
    string? ICoreBrowserInstanceManager.SelectedPageId => CurrentInstance?.SelectedPageId;

    /// <summary>
    /// 底层浏览器上下文（如果当前实例存在）。<br/>
    /// Underlying browser context for the current instance, if present.
    /// </summary>
    public IBrowserContext? BrowserContext => CurrentInstance?.BrowserContext;

    /// <summary>
    /// 当前活动页面对象（如果存在）。<br/>
    /// The currently active page object, if any.
    /// </summary>
    public IPage? ActivePage => CurrentInstance?.GetSelectedPage();

    /// <summary>
    /// 当前选中的标签页 Id（如果存在）。<br/>
    /// Currently selected tab id, if any.
    /// </summary>
    public string? SelectedPageId => CurrentInstance?.SelectedPageId;

    /// <summary>
    /// 管理器内所有页面的总数。<br/>
    /// Total number of pages across all managed instances.
    /// </summary>
    public int TotalPageCount => _instances.Values.Sum(i => i.Pages.Count);

    /// <summary>
    /// 当前活动页面的 URL（如果存在）。<br/>
    /// URL of the currently active page, if any.
    /// </summary>
    public string? CurrentUrl => ActivePage?.Url;

    // Provide a Uri-typed accessor to satisfy tools that prefer System.Uri for URL properties.
    // Keep the string-typed CurrentUrl for compatibility with Contracts interfaces.
    /// <summary>
    /// 当前活动页面的 Uri 表示（如果 URL 可解析）。<br/>
    /// Uri representation of the current active page URL if parsable.
    /// </summary>
    public Uri? CurrentUri
    {
        get
        {
            var url = ActivePage?.Url;
            if (string.IsNullOrWhiteSpace(url)) return null;
            return Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri : null;
        }
    }

    // IBrowserInstanceManager compatibility
    /// <summary>
    /// 获取当前管理的浏览器实例信息列表（供 UI/外部使用）。<br/>
    /// Get a list of current managed browser instance infos (for UI/external use).
    /// </summary>
    public IReadOnlyList<BrowserInstanceInfo> GetInstances() => Instances.Select(i => new BrowserInstanceInfo(i.InstanceId, i.DisplayName, i.OwnerId, "#ccc", string.Equals(i.InstanceId, CurrentInstanceId, StringComparison.OrdinalIgnoreCase), i.Pages.Count)).ToArray();

    /// <summary>
    /// 获取属于指定插件拥有者的实例 Id 列表。<br/>
    /// Get the list of instance ids owned by the specified plugin.
    /// </summary>
    /// <param name="pluginId">插件标识 / plugin id.</param>
    public IReadOnlyList<string> GetPluginInstanceIds(string pluginId) => Instances.Where(i => string.Equals(i.OwnerId, pluginId, StringComparison.OrdinalIgnoreCase)).Select(i => i.InstanceId).ToArray();

    /// <summary>
    /// 获取实例颜色（用于 UI 显示，当前返回占位颜色）。<br/>
    /// Get the instance color (used for UI; currently returns a placeholder color).
    /// </summary>
    /// <param name="instanceId">实例 Id / instance id.</param>
    public string? GetInstanceColor(string instanceId) => "#ccc";

    /// <summary>
    /// 根据实例 Id 获取其浏览器上下文（如果存在）。<br/>
    /// Get the browser context for the given instance id if present.
    /// </summary>
    /// <param name="instanceId">实例 Id / instance id.</param>
    public IBrowserContext? GetBrowserContext(string instanceId) => _instances.TryGetValue(instanceId, out var inst) ? inst.BrowserContext : null;

    /// <summary>
    /// 获取指定实例的当前活动页面（如果存在）。<br/>
    /// Get the currently active page of the specified instance, if any.
    /// </summary>
    /// <param name="instanceId">实例 Id / instance id.</param>
    public IPage? GetActivePage(string instanceId) => _instances.TryGetValue(instanceId, out var inst2) ? inst2.GetSelectedPage() : null;

    // Note: this core class does not implement IBrowserInstanceManager directly.
    // A host-level adapter implements IBrowserInstanceManager and delegates
    // to this core class.

    /// <summary>
    /// 创建一个新的浏览器实例并启动 Playwright 持久化上下文。<br/>
    /// Create a new browser instance and start a Playwright persistent context.
    /// </summary>
    /// <param name="ownerId">实例拥有者标识（通常为插件 id） / owner id, typically a plugin id.</param>
    /// <param name="displayName">实例显示名称 / display name for the instance.</param>
    /// <param name="userDataDir">用户数据目录根路径 / user data directory root path.</param>
    /// <param name="headless">是否无头运行 / whether to run headless.</param>
    /// <param name="previewInstanceId">可选的预览/指定实例 id / optional preview/specified instance id.</param>
    /// <returns>创建的实例 Id。<br/>The created instance id.</returns>
    public async Task<string> CreateAsync(string ownerId, string displayName, string userDataDir, bool headless = true, string? previewInstanceId = null)
    {
        // use a shorter id to keep per-instance directory names readable
        string id;
        if (!string.IsNullOrWhiteSpace(previewInstanceId))
        {
            id = previewInstanceId!;
        }
        else
        {
            var shortId = Guid.NewGuid().ToString("N").Substring(0, 8);
            id = $"{ownerId}-{shortId}";
        }
        var inst = new PlaywrightInstance(_loggerFactory?.CreateLogger<PlaywrightInstance>() ?? NullLogger<PlaywrightInstance>.Instance, id, displayName, ownerId);
        if (!_instances.TryAdd(id, inst))
            throw new InvalidOperationException("Failed to add instance");
        // Create a per-instance user-data directory to avoid Playwright conflicts when multiple
        // persistent contexts run simultaneously. Use a subfolder under the provided root.
        var instanceUserDataDir = Path.Combine(userDataDir, id);
        Directory.CreateDirectory(instanceUserDataDir);
        await inst.InitializeAsync(instanceUserDataDir, headless);
        _currentInstanceId = id;
        _logger.LogInformation("Created instance {Id}", id);
        return id;
    }

    // High-level helpers used by Web controllers
    /// <summary>
    /// 为插件创建浏览器实例的便捷方法，使用程序设置作为默认值。<br/>
    /// Convenience helper to create a browser instance for a plugin using program settings as defaults.
    /// </summary>
    /// <param name="ownerPluginId">插件 Id / owner plugin id.</param>
    /// <param name="displayName">可选显示名称 / optional display name.</param>
    /// <param name="userDataDirectory">可选用户数据目录 / optional user data directory.</param>
    /// <param name="previewInstanceId">可选预览实例 Id / optional preview instance id.</param>
    /// <returns>已创建的实例 Id / the created instance id.</returns>
    public async Task<string> CreateBrowserInstanceAsync(string ownerPluginId, string? displayName = null, string? userDataDirectory = null, string? previewInstanceId = null)
    {
        var settings = _settingsProvider is null ? new ProgramSettings() : await _settingsProvider.GetAsync();
        var userData = string.IsNullOrWhiteSpace(userDataDirectory) ? settings.UserDataDirectory : userDataDirectory!;
        var name = string.IsNullOrWhiteSpace(displayName) ? "Browser" : displayName!;
        return await CreateAsync(ownerPluginId, name, userData, settings.Headless, previewInstanceId);
    }

    /// <summary>
    /// 在当前实例中创建一个新标签页并可选导航至指定 URL。若不存在实例则先创建一个。<br/>
    /// Create a new tab in the current instance and optionally navigate to the specified URL. Creates an instance if none exists.
    /// </summary>
    /// <param name="url">可选导航 URL / optional URL to navigate to.</param>
    /// <returns>表示操作完成的异步任务。<br/>A Task representing completion of the operation.</returns>
    public async Task CreateTabAsync(string? url = null)
    {
        if (CurrentInstance is null)
            await CreateBrowserInstanceAsync("ui");

        await CurrentInstance!.CreateTabAsync(url);
    }

    /// <summary>
    /// 在所有实例中查找并选择指定标签页（将该实例设为当前实例）。<br/>
    /// Find and select the specified tab across instances (set that instance as current).
    /// </summary>
    /// <param name="tabId">标签页 Id / tab id to select.</param>
    /// <returns>如果选择成功则返回 true。<br/>True when selection succeeded.</returns>
    public async Task<bool> SelectPageAsync(string tabId)
    {
        foreach (var kv in _instances)
        {
            if (kv.Value.Pages.Any(p => kv.Value.TabIds.Contains(tabId)))
            {
                _currentInstanceId = kv.Key;
                return kv.Value.SelectPage(tabId);
            }
        }

        return false;
    }

    // keep public helper for other code
    /// <summary>
    /// 将指定浏览器实例设为当前实例（如果存在）。<br/>
    /// Set the specified browser instance as the current instance if it exists.
    /// </summary>
    /// <param name="instanceId">实例 Id / instance id to select.</param>
    /// <param name="cancellationToken">可选的取消令牌 / optional cancellation token.</param>
    /// <returns>返回是否选择成功。<br/>Returns whether the selection succeeded.</returns>
    public Task<bool> SelectBrowserInstanceAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        if (!_instances.ContainsKey(instanceId)) return Task.FromResult(false);
        _currentInstanceId = instanceId;
        return Task.FromResult(true);
    }

    /// <summary>
    /// 关闭指定的浏览器实例（异步）。<br/>
    /// Close the specified browser instance asynchronously.
    /// </summary>
    /// <param name="instanceId">要关闭的实例 Id / instance id to close.</param>
    /// <param name="cancellationToken">可选取消令牌 / optional cancellation token.</param>
    /// <returns>返回是否成功关闭。<br/>Returns whether the close succeeded.</returns>
    public async Task<bool> CloseBrowserInstanceAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        return await RemoveAsync(instanceId);
    }

    /// <summary>
    /// 捕获当前活动页面的截图（占位实现，当前返回 null）。<br/>
    /// Capture a screenshot of the current active page (placeholder; currently returns null).
    /// </summary>
    public Task<byte[]?> CaptureScreenshotAsync() => Task.FromResult<byte[]?>(null);

    /// <summary>
    /// 设置当前实例的视口大小（占位实现）。<br/>
    /// Set the viewport size for the current instance (placeholder).
    /// </summary>
    /// <param name="width">宽度 / width.</param>
    /// <param name="height">高度 / height.</param>
    /// <returns>表示操作完成的任务 / A Task representing completion of the operation.</returns>
    public Task SetViewportSizeAsync(int width, int height) => Task.CompletedTask;

    /// <summary>
    /// 更新启动设置并在需要时重新创建实例以应用新的用户数据目录或是否无头运行的设置。<br/>
    /// Update launch settings and recreate instances if necessary to apply new user-data directory or headless settings.
    /// </summary>
    /// <param name="primaryUserDataDirectory">主用户数据目录路径 / primary user data directory path.</param>
    /// <param name="isHeadless">是否无头运行 / whether to run headless.</param>
    /// <param name="forceReload">是否强制重载 / force reload flag.</param>
    /// <returns>表示操作完成的异步任务 / A Task representing completion of the operation.</returns>
    public async Task UpdateLaunchSettingsAsync(string primaryUserDataDirectory, bool isHeadless, bool forceReload = false)
    {
        // If no instances, nothing to do.
        if (!_instances.Any())
            return;

        // Normalize root
        var root = string.IsNullOrWhiteSpace(primaryUserDataDirectory) ? ProgramSettings.GetDefaultUserDataDirectoryPath() : Path.GetFullPath(primaryUserDataDirectory);

        // Capture existing instances info
        var existing = _instances.Values.Select(i => new { i.InstanceId, i.DisplayName, i.OwnerId, UserData = i.UserDataDirectoryPath, WasSelected = string.Equals(i.InstanceId, _currentInstanceId, StringComparison.OrdinalIgnoreCase) }).ToList();

        // Remove all instances
        foreach (var item in existing)
        {
            if (_instances.ContainsKey(item.InstanceId))
            {
                try { await RemoveAsync(item.InstanceId); } catch { }
            }
        }

        // Recreate instances using same ids under the new root and with new headless flag
        string? newCurrent = null;
        foreach (var item in existing)
        {
            try
            {
                var id = await CreateAsync(item.OwnerId, item.DisplayName ?? "Browser", root, headless: isHeadless, previewInstanceId: item.InstanceId);
                if (item.WasSelected)
                    newCurrent = id;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to recreate instance {Id} during launch settings update", item.InstanceId);
            }
        }

        if (!string.IsNullOrWhiteSpace(newCurrent))
            _currentInstanceId = newCurrent;
    }

    /// <summary>
    /// 关闭指定标签页并从实例中移除。<br/>
    /// Close the specified tab and remove it from the instance.
    /// </summary>
    /// <param name="tabId">标签页 Id / tab id to close.</param>
    /// <returns>返回是否成功关闭。<br/>Returns whether the tab was closed.</returns>
    public async Task<bool> CloseTabAsync(string tabId)
    {
        foreach (var kv in _instances)
        {
            if (kv.Value.TabIds.Contains(tabId))
                return await kv.Value.CloseTabAsync(tabId);
        }

        return false;
    }

    /// <summary>
    /// 更新指定实例的设置（例如用户数据目录）。<br/>
    /// Update settings for the specified instance (e.g., user data directory).
    /// </summary>
    /// <param name="request">包含更新信息的请求对象 / update request object.</param>
    /// <param name="cancellationToken">可选的取消令牌 / optional cancellation token.</param>
    /// <returns>返回是否更新成功。<br/>Returns whether the update succeeded.</returns>
    public async Task<bool> UpdateInstanceSettingsAsync(BrowserInstanceSettingsUpdateRequest request, CancellationToken cancellationToken = default)
    {
        if (!_instances.TryGetValue(request.InstanceId, out var inst)) return false;
        inst.UserDataDirectoryPath = request.UserDataDirectory;
        // Note: real migration/viewport handling omitted for brevity
        return true;
    }

    // Long-parameter overload removed. Use BrowserInstanceSettingsUpdateRequest instead.

    /// <summary>
    /// 将当前实例的视口大小与给定参数同步（占位实现）。<br/>
    /// Sync the current instance viewport size to given dimensions (placeholder).
    /// </summary>
    /// <param name="width">宽度 / width.</param>
    /// <param name="height">高度 / height.</param>
    /// <returns>表示操作完成的异步任务 / A Task representing completion of the operation.</returns>
    public async Task SyncCurrentInstanceViewportAsync(int width, int height, CancellationToken cancellationToken = default)
    {
        // noop for now
        await Task.CompletedTask;
    }

    /// <summary>
    /// 导航当前活动页面到指定 URL（若 URL 不是绝对链接将使用默认搜索模板处理）。<br/>
    /// Navigate the current active page to the specified URL. Non-absolute URLs will be treated via the default search template.
    /// </summary>
    /// <param name="url">目标 URL / target URL.</param>
    /// <returns>表示导航操作完成的异步任务 / A Task representing completion of the navigation.</returns>
    public async Task NavigateAsync(string url)
    {
        if (CurrentInstance?.GetSelectedPage() is IPage page)
        {
            var target = url;
            if (!Uri.IsWellFormedUriString(url, UriKind.Absolute))
                target = ProgramSettings.DefaultSearchUrlTemplate.Replace("{query}", Uri.EscapeDataString(url));

            try { await page.GotoAsync(target); } catch { }
        }
    }

    /// <summary>
    /// 在当前活动页面执行后退操作。<br/>
    /// Perform a browser back navigation on the current active page.
    /// </summary>
    /// <returns>表示后退操作完成的异步任务 / A Task representing completion of the back navigation.</returns>
    public async Task GoBackAsync()
    {
        if (CurrentInstance?.GetSelectedPage() is IPage page) { try { await page.GoBackAsync(); } catch { } }
    }

    /// <summary>
    /// 在当前活动页面执行前进操作。<br/>
    /// Perform a browser forward navigation on the current active page.
    /// </summary>
    /// <returns>表示前进操作完成的异步任务 / A Task representing completion of the forward navigation.</returns>
    public async Task GoForwardAsync()
    {
        if (CurrentInstance?.GetSelectedPage() is IPage page) { try { await page.GoForwardAsync(); } catch { } }
    }

    /// <summary>
    /// 重新加载当前活动页面。<br/>
    /// Reload the current active page.
    /// </summary>
    /// <returns>表示重新加载操作完成的异步任务 / A Task representing completion of the reload.</returns>
    public async Task ReloadAsync()
    {
        if (CurrentInstance?.GetSelectedPage() is IPage page) { try { await page.ReloadAsync(); } catch { } }
    }

    /// <summary>
    /// 获取当前活动页面的标题（若存在）。<br/>
    /// Get the title of the current active page, if any.
    /// </summary>
    /// <returns>页面标题或 null。<br/>The page title or null.</returns>
    public async Task<string?> GetTitleAsync()
    {
        if (CurrentInstance?.GetSelectedPage() is IPage page) { try { return await page.TitleAsync(); } catch { } }
        return null;
    }

    /// <summary>
    /// 枚举所有实例中的标签页并返回其摘要信息列表。<br/>
    /// Enumerate tabs across all instances and return a list of tab summary infos.
    /// </summary>
    /// <returns>包含标签页摘要信息的异步任务 / A Task returning a list of tab summary infos.</returns>
    public async Task<IReadOnlyList<BrowserTabInfo>> GetTabsAsync()
    {
        var tabs = new List<BrowserTabInfo>();
        foreach (var inst in _instances.Values)
        {
            foreach (var id in inst.TabIds)
            {
                var page = inst.GetPageById(id);
                tabs.Add(new BrowserTabInfo(id, page?.TitleAsync().GetAwaiter().GetResult(), page?.Url, inst.SelectedPageId == id, inst.OwnerId));
            }
        }

        return tabs;
    }

    /// <summary>
    /// 获取当前实例的视口设置响应对象（用于 UI）。<br/>
    /// Get the viewport settings response object for the current instance (for UI).
    /// </summary>
    /// <returns>视口设置响应对象 / Viewport settings response object.</returns>
    public BrowserInstanceViewportSettingsResponse GetCurrentInstanceViewportSettings()
    {
        return new BrowserInstanceViewportSettingsResponse(1280, 800, "Auto");
    }

    /// <summary>
    /// 获取指定实例的设置响应对象（如果存在）。<br/>
    /// Get settings response for the specified instance if it exists.
    /// </summary>
    /// <param name="instanceId">实例 Id / instance id.</param>
    /// <returns>表示请求实例设置的异步任务，可能为 null / A Task returning the instance settings response, or null.</returns>
    public async Task<BrowserInstanceSettingsResponse?> GetInstanceSettingsAsync(string instanceId)
    {
        if (!_instances.TryGetValue(instanceId, out var inst)) return null;
        return new BrowserInstanceSettingsResponse(inst.InstanceId, inst.UserDataDirectoryPath, inst.UserDataDirectoryPath);
    }

    /// <summary>
    /// 尝试根据 id 获取核心实例。<br/>
    /// Try to retrieve a core instance by id.
    /// </summary>
    /// <param name="id">实例 id / instance id.</param>
    /// <param name="inst">输出参数，当返回 true 时包含实例 / out parameter that will contain the instance when true.</param>
    public bool TryGet(string id, out ICoreBrowserInstance inst) => _instances.TryGetValue(id, out inst);

    /// <summary>
    /// 获取指定 id 的实例或返回 null。<br/>
    /// Get the instance for the specified id or return null.
    /// </summary>
    /// <param name="instanceId">实例 id / instance id.</param>
    public ICoreBrowserInstance? GetInstance(string instanceId) => _instances.TryGetValue(instanceId, out var inst) ? inst : null;

    /// <summary>
    /// 关闭并移除指定实例。<br/>
    /// Close and remove the specified instance.
    /// </summary>
    /// <param name="instanceId">实例 id / instance id to close.</param>
    public async Task<bool> CloseInstanceAsync(string instanceId)
    {
        return await RemoveAsync(instanceId);
    }

    /// <summary>
    /// 从管理集合中移除实例并关闭其资源。<br/>
    /// Remove an instance from management and close its resources.
    /// </summary>
    /// <param name="id">实例 id / instance id to remove.</param>
    public async Task<bool> RemoveAsync(string id)
    {
        if (!_instances.TryRemove(id, out var inst))
            return false;

        await inst.CloseAsync();
        _logger.LogInformation("Removed instance {Id}", id);
        return true;
    }
}
