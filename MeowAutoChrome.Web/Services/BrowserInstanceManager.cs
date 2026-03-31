// Avoid direct dependency on Core model types in Web layer; use Web DTOs instead.
using MeowAutoChrome.Core;
using MeowAutoChrome.Core.Interface;
using Microsoft.Playwright;

namespace MeowAutoChrome.Web.Services;

/// <summary>
/// Web 层的浏览器实例管理器，作为对 Core 管理器的包装并提供 Web DTO。<br/>
/// Web-layer browser instance manager that wraps the Core manager and exposes Web DTOs.
/// </summary>
public class BrowserInstanceManager
{
    private readonly BrowserInstanceManagerCore _core;
    private readonly IProgramSettingsProvider _settingsProvider;
    private readonly ILogger<BrowserInstanceManager> _logger;

    /// <summary>
    /// 创建 BrowserInstanceManager 的实例，包装 Core 的 BrowserInstanceManagerCore。<br/>
    /// Create a BrowserInstanceManager that wraps the core BrowserInstanceManagerCore.
    /// </summary>
    /// <param name="core">核心实例管理器 / core instance manager.</param>
    /// <param name="settingsProvider">程序设置提供者 / program settings provider.</param>
    /// <param name="logger">日志记录器 / logger.</param>
    public BrowserInstanceManager(BrowserInstanceManagerCore core, IProgramSettingsProvider settingsProvider, ILogger<BrowserInstanceManager> logger)
    {
        _core = core;
        _settingsProvider = settingsProvider;
        _logger = logger;
    }

    /// <summary>
    /// 当前选中的实例 Id（如果存在）。<br/>
    /// The current selected instance id, if any.
    /// </summary>
    public string CurrentInstanceId
    {
        get
        {
            var first = _core.Instances.FirstOrDefault();
            return first?.InstanceId ?? string.Empty;
        }
    }

    /// <summary>
    /// 当前实例的动态表示（可能为 null）。<br/>
    /// Dynamic representation of the current instance (may be null).
    /// </summary>
    public dynamic? CurrentInstance => _core.Instances.FirstOrDefault();

    /// <summary>
    /// 当前实例的 BrowserContext（如有）。<br/>
    /// BrowserContext for the current instance, if available.
    /// </summary>
    public IBrowserContext? BrowserContext => _core.Instances.FirstOrDefault()?.BrowserContext;

    /// <summary>
    /// 当前活动页面（如果存在）。<br/>
    /// The currently active page, if any.
    /// </summary>
    public IPage? ActivePage => BrowserContext?.Pages.FirstOrDefault();

    /// <summary>
    /// 已选页面 Id（占位，可能为 null）。<br/>
    /// Selected page id (placeholder, may be null).
    /// </summary>
    public string? SelectedPageId => null;

    /// <summary>
    /// 当前是否以无头模式运行。<br/>
    /// Whether the current instance(s) run in headless mode.
    /// </summary>
    public bool IsHeadless => _core.Instances.FirstOrDefault()?.IsHeadless ?? false;

    /// <summary>
    /// 当前页面的 URL（如果可用）。<br/>
    /// Current page URL, if available.
    /// </summary>
    public string? CurrentUrl => ActivePage?.Url;

    /// <summary>
    /// 总页签数量（跨所有实例）。<br/>
    /// Total page count across all instances.
    /// </summary>
    public int TotalPageCount => _core.Instances.Sum(i => i.BrowserContext?.Pages.Count ?? 0);

    /// <summary>
    /// 获取当前所有实例的 DTO 列表。<br/>
    /// Get a list of DTOs representing current instances.
    /// </summary>
    /// <returns>浏览器实例信息 DTO 列表 / list of browser instance info DTOs.</returns>
    public IReadOnlyList<Models.BrowserInstanceInfoDto> GetInstances()
    {
        return _core.Instances.Select(i => new Models.BrowserInstanceInfoDto(i.InstanceId, i.DisplayName, i.UserDataDirectoryPath, "#000000", string.Equals(i.InstanceId, CurrentInstanceId, StringComparison.OrdinalIgnoreCase), i.BrowserContext?.Pages.Count ?? 0)).ToArray();
    }

    /// <summary>
    /// 向后兼容的别名，返回 DTO 列表。<br/>
    /// Backwards-compatible alias returning DTO list.
    /// </summary>
    public IReadOnlyList<Models.BrowserInstanceInfoDto> GetInstancesDto() => GetInstances();

    /// <summary>
    /// 获取指定实例的设置 DTO（异步）。<br/>
    /// Get instance settings DTO asynchronously for the specified instance.
    /// </summary>
    /// <param name="instanceId">实例 Id / instance id.</param>
    /// <returns>实例设置 DTO 或 null / instance settings DTO or null.</returns>
    public async Task<Models.BrowserInstanceSettingsResponseDto?> GetInstanceSettingsAsync(string instanceId)
    {
        var inst = _core.Instances.FirstOrDefault(i => i.InstanceId == instanceId);
        if (inst == null) return null;
        return new Models.BrowserInstanceSettingsResponseDto(inst.InstanceId, inst.DisplayName, inst.UserDataDirectoryPath);
    }

    /// <summary>
    /// 获取当前实例的视口设置（同步）。<br/>
    /// Get the current instance viewport settings (sync).
    /// </summary>
    public Core.Models.BrowserInstanceViewportSettingsResponse GetCurrentInstanceViewportSettings()
    {
        return new Core.Models.BrowserInstanceViewportSettingsResponse(1280, 800, "Auto");
    }

    /// <summary>
    /// 获取当前实例的视口设置（异步 DTO）。<br/>
    /// Get the current instance viewport settings as an async DTO.
    /// </summary>
    /// <returns>视口设置 DTO / viewport settings DTO.</returns>
    public Task<Models.BrowserInstanceViewportSettingsResponseDto> GetCurrentInstanceViewportSettingsAsync()
        => Task.FromResult(new Models.BrowserInstanceViewportSettingsResponseDto(1280, 800, "Auto"));

    /// <summary>
    /// 获取由指定插件创建的实例 Id 列表（占位）。<br/>
    /// Get instance ids created by the specified plugin (placeholder).
    /// </summary>
    public IReadOnlyList<string> GetPluginInstanceIds(string pluginId)
        => Array.Empty<string>();

    /// <summary>
    /// 获取实例对应的颜色（占位）。<br/>
    /// Get a color associated with the instance (placeholder).
    /// </summary>
    public string? GetInstanceColor(string instanceId) => "#000000";

    /// <summary>
    /// 获取指定实例的 BrowserContext（如有）。<br/>
    /// Get the BrowserContext for the specified instance, if available.
    /// </summary>
    /// <param name="instanceId">实例 Id / instance id.</param>
    /// <returns>BrowserContext 或 null / BrowserContext or null.</returns>
    public IBrowserContext? GetBrowserContext(string instanceId)
        => _core.Instances.FirstOrDefault(i => i.InstanceId == instanceId)?.BrowserContext;

    /// <summary>
    /// 获取指定实例的活动页面（如有）。<br/>
    /// Get the active page for the specified instance, if any.
    /// </summary>
    /// <param name="instanceId">实例 Id / instance id.</param>
    /// <returns>页面或 null / page or null.</returns>
    public IPage? GetActivePage(string instanceId)
        => _core.Instances.FirstOrDefault(i => i.InstanceId == instanceId)?.BrowserContext?.Pages.FirstOrDefault();

    /// <summary>
    /// 创建浏览器实例并返回实例 Id。<br/>
    /// Create a browser instance and return the instance id.
    /// </summary>
    /// <param name="ownerPluginId">拥有者插件 Id / owner plugin id.</param>
    /// <param name="displayName">可选显示名称 / optional display name.</param>
    /// <param name="userDataDirectory">可选用户数据目录 / optional user data directory.</param>
    /// <param name="previewInstanceId">可选预览实例 Id / optional preview instance id.</param>
    /// <param name="cancellationToken">取消令牌 / cancellation token.</param>
    /// <returns>新实例 Id / new instance id.</returns>
    public async Task<string> CreateBrowserInstanceAsync(string ownerPluginId, string? displayName = null, string? userDataDirectory = null, string? previewInstanceId = null, CancellationToken cancellationToken = default)
    {
        var name = string.IsNullOrWhiteSpace(displayName) ? ownerPluginId : displayName;
        var settings = _settingsProvider is null ? new Core.Struct.ProgramSettings() : await _settingsProvider.GetAsync();
        var dir = string.IsNullOrWhiteSpace(userDataDirectory) ? settings.UserDataDirectory : Path.GetFullPath(userDataDirectory);
        return await _core.CreateAsync(ownerPluginId, name, dir, headless: settings.Headless, previewInstanceId);
    }

    /// <summary>
    /// 删除指定的浏览器实例。<br/>
    /// Remove the specified browser instance.
    /// </summary>
    /// <param name="instanceId">实例 Id / instance id.</param>
    /// <param name="cancellationToken">取消令牌 / cancellation token.</param>
    /// <returns>是否成功删除 / whether removal succeeded.</returns>
    public Task<bool> RemoveBrowserInstanceAsync(string instanceId, CancellationToken cancellationToken = default)
        => _core.RemoveAsync(instanceId);

    /// <summary>
    /// 选择指定的浏览器实例（如果存在）。<br/>
    /// Select the specified browser instance if it exists.
    /// </summary>
    /// <param name="instanceId">实例 Id / instance id.</param>
    /// <param name="cancellationToken">取消令牌 / cancellation token.</param>
    /// <returns>是否选择成功 / whether selection succeeded.</returns>
    public Task<bool> SelectBrowserInstanceAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        var ok = _core.TryGet(instanceId, out var inst);
        return Task.FromResult(ok);
    }

    /// <summary>
    /// 预览新实例（返回实例 Id 与用户数据目录）。<br/>
    /// Preview a new instance (returns instance id and user data directory).
    /// </summary>
    public Task<(string InstanceId, string UserDataDirectory)> PreviewNewInstanceAsync(string? ownerPluginId, string? userDataDirectoryRoot)
        => _core.PreviewNewInstanceAsync(ownerPluginId ?? "ui", userDataDirectoryRoot);

    /// <summary>
    /// 获取所有标签页信息的 DTO 列表。<br/>
    /// Get a list of DTOs for all tabs.
    /// </summary>
    /// <returns>标签信息 DTO 列表 / list of tab info DTOs.</returns>
    public async Task<IReadOnlyList<Models.BrowserTabInfoDto>> GetTabsAsync()
    {
        var tabs = new List<Models.BrowserTabInfoDto>();
        foreach (var inst in _core.Instances)
        {
            var pages = inst.BrowserContext?.Pages ?? new List<IPage>();
            foreach (var page in pages)
            {
                tabs.Add(new Models.BrowserTabInfoDto(Guid.NewGuid().ToString("N"), await SafeTitleAsync(page), page.Url, false, null));
            }
        }

        return tabs;

        static async Task<string?> SafeTitleAsync(IPage p)
        {
            try { return await p.TitleAsync(); } catch { return null; }
        }
    }

    /// <summary>
    /// 选择页面（占位，暂未实现）。<br/>
    /// Select a page (placeholder, not implemented).
    /// </summary>
    public Task<bool> SelectPageAsync(string tabId) => Task.FromResult(false);
    /// <summary>
    /// 关闭标签页（占位，暂未实现）。<br/>
    /// Close a tab (placeholder, not implemented).
    /// </summary>
    public Task<bool> CloseTabAsync(string tabId) => Task.FromResult(false);

    /// <summary>
    /// 关闭指定的浏览器实例。<br/>
    /// Close the specified browser instance.
    /// </summary>
    /// <param name="instanceId">实例 Id / instance id.</param>
    /// <param name="cancellationToken">取消令牌 / cancellation token.</param>
    /// <returns>是否成功关闭 / whether close succeeded.</returns>
    public async Task<bool> CloseBrowserInstanceAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(instanceId)) return false;
        return await _core.RemoveAsync(instanceId);
    }

    /// <summary>
    /// 创建新标签页（占位实现）。<br/>
    /// Create a new tab (placeholder implementation).
    /// </summary>
    public Task CreateTabAsync(string? url = null) => Task.CompletedTask;
    /// <summary>
    /// 获取当前页面标题（占位实现）。<br/>
    /// Get current page title (placeholder implementation).
    /// </summary>
    public Task<string?> GetTitleAsync() => Task.FromResult<string?>(null);
    /// <summary>
    /// 导航到指定 URL（占位实现）。<br/>
    /// Navigate to the specified URL (placeholder implementation).
    /// </summary>
    public Task NavigateAsync(string url) => Task.CompletedTask;
    /// <summary>
    /// 后退（占位实现）。<br/>
    /// Go back (placeholder implementation).
    /// </summary>
    public Task GoBackAsync() => Task.CompletedTask;
    /// <summary>
    /// 前进（占位实现）。<br/>
    /// Go forward (placeholder implementation).
    /// </summary>
    public Task GoForwardAsync() => Task.CompletedTask;
    /// <summary>
    /// 重新加载（占位实现）。<br/>
    /// Reload (placeholder implementation).
    /// </summary>
    public Task ReloadAsync() => Task.CompletedTask;
    /// <summary>
    /// 捕获截图（占位实现）。<br/>
    /// Capture a screenshot (placeholder implementation).
    /// </summary>
    public Task<byte[]?> CaptureScreenshotAsync() => Task.FromResult<byte[]?>(null);
    /// <summary>
    /// 设置视口大小（占位实现）。<br/>
    /// Set viewport size (placeholder implementation).
    /// </summary>
    public Task SetViewportSizeAsync(int width, int height) => Task.CompletedTask;

    /// <summary>
    /// 更新启动设置（主用户数据目录与 headless 标志），必要时重建实例。<br/>
    /// Update launch settings (primary user data directory and headless flag), recreating instances if needed.
    /// </summary>
    public async Task UpdateLaunchSettingsAsync(string primaryUserDataDirectory, bool isHeadless, bool forceReload = false)
    {
        // Delegate to core manager which handles instance recreation.
        await _core.UpdateLaunchSettingsAsync(primaryUserDataDirectory, isHeadless, forceReload);

        // After recreating instances we should refresh plugin host state if available
        try
        {
            var pluginHost = System.AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetTypes())
                .SelectMany(t => t)
                .FirstOrDefault(t => typeof(IPluginHostCore).IsAssignableFrom(t));
            // We don't have a direct reference here; callers should ensure plugin host reloads as needed.
        }
        catch { }
    }
    /// <summary>
    /// 更新实例设置（占位实现）。<br/>
    /// Update instance settings (placeholder implementation).
    /// </summary>
    public Task<bool> UpdateInstanceSettingsAsync(Core.Models.BrowserInstanceSettingsUpdateRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    // Long-parameter overload removed; callers should use BrowserInstanceSettingsUpdateRequest instead.
    /// <summary>
    /// 同步当前实例视口（占位实现）。<br/>
    /// Sync current instance viewport (placeholder implementation).
    /// </summary>
    public Task SyncCurrentInstanceViewportAsync(int width, int height, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
