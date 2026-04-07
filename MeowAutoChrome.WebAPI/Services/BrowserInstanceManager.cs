using MeowAutoChrome.Core;
using MeowAutoChrome.Core.Interface;
using Microsoft.Playwright;
using MeowAutoChrome.WebAPI.Models;

namespace MeowAutoChrome.WebAPI.Services;

/// <summary>
/// WebAPI 层的浏览器实例适配服务，用于把 Core 能力转换为 API 可直接消费的模型。<br/>
/// WebAPI-level browser instance adapter that converts Core capabilities into models directly consumable by the API layer.
/// </summary>
public class BrowserInstanceManager(BrowserInstanceManagerCore core, IProgramSettingsProvider settingsProvider, ILogger<BrowserInstanceManager> logger)
{

    /// <summary>
    /// 当前选中的实例 ID。<br/>
    /// Identifier of the currently selected instance.
    /// </summary>
    public string CurrentInstanceId
        => core.CurrentInstanceId;

    /// <summary>
    /// 当前选中的实例对象。<br/>
    /// Currently selected instance object.
    /// </summary>
    public dynamic? CurrentInstance => core.CurrentInstance;

    /// <summary>
    /// 当前实例对应的浏览器上下文。<br/>
    /// Browser context associated with the current instance.
    /// </summary>
    public IBrowserContext? BrowserContext => core.BrowserContext;

    /// <summary>
    /// 当前活动页面。<br/>
    /// Current active page.
    /// </summary>
    public IPage? ActivePage => core.ActivePage;

    /// <summary>
    /// 当前选中页面 ID。<br/>
    /// Identifier of the selected page.
    /// </summary>
    public string? SelectedPageId => core.SelectedPageId;

    /// <summary>
    /// 当前运行模式是否为 Headless。<br/>
    /// Whether the current runtime mode is headless.
    /// </summary>
    public bool IsHeadless => core.IsHeadless;

    /// <summary>
    /// 当前活动页面 URL。<br/>
    /// URL of the current active page.
    /// </summary>
    public string? CurrentUrl => core.CurrentUrl;

    /// <summary>
    /// 全部实例中的页面总数。<br/>
    /// Total number of pages across all instances.
    /// </summary>
    public int TotalPageCount => core.TotalPageCount;

    /// <summary>
    /// 获取全部实例的 API 视图。<br/>
    /// Get the API view of all browser instances.
    /// </summary>
    /// <returns>实例信息列表。<br/>List of instance information.</returns>
    public IReadOnlyList<BrowserInstanceInfoDto> GetInstances() => [.. core.GetInstances()
            .Select(i => new BrowserInstanceInfoDto(
                i.Id,
                i.Name,
                core.GetInstance(i.Id)?.UserDataDirectoryPath,
                GetInstanceColor(i.Id),
                i.IsSelected,
                i.PageCount))];

    /// <summary>
    /// 获取全部实例的 DTO 列表。<br/>
    /// Get the DTO list for all instances.
    /// </summary>
    /// <returns>实例 DTO 列表。<br/>List of instance DTOs.</returns>
    public IReadOnlyList<BrowserInstanceInfoDto> GetInstancesDto() => GetInstances();

    /// <summary>
    /// 获取指定实例的设置响应模型。<br/>
    /// Get the settings response model for the specified instance.
    /// </summary>
    /// <param name="instanceId">实例 ID。<br/>Instance id.</param>
    /// <returns>实例设置响应；若不存在则返回空。<br/>Instance settings response, or null when the instance does not exist.</returns>
    public async Task<BrowserInstanceSettingsResponseDto?> GetInstanceSettingsAsync(string instanceId)
    {
        var inst = core.GetInstance(instanceId);
        if (inst is null)
            return null;

        var settings = await settingsProvider.GetAsync();
        return new BrowserInstanceSettingsResponseDto(
            inst.InstanceId,
            inst.DisplayName,
            inst.UserDataDirectoryPath,
            string.Equals(inst.InstanceId, core.CurrentInstanceId, StringComparison.OrdinalIgnoreCase),
            new BrowserInstanceSettingsViewportDto(1280, 800, false, false),
            new BrowserInstanceSettingsUserAgentDto(false, false, null, settings.UserAgent, settings.UserAgent));
    }

    /// <summary>
    /// 获取当前实例的视口设置。<br/>
    /// Get viewport settings for the current instance.
    /// </summary>
    /// <returns>Core 视口设置模型。<br/>Core viewport settings model.</returns>
    public Core.Models.BrowserInstanceViewportSettingsResponse GetCurrentInstanceViewportSettings() => core.GetCurrentInstanceViewportSettings();

    /// <summary>
    /// 获取当前实例的 API 视口设置响应。<br/>
    /// Get the API viewport settings response for the current instance.
    /// </summary>
    /// <returns>API 视口设置响应。<br/>API viewport settings response.</returns>
    public Task<BrowserInstanceViewportSettingsResponseDto> GetCurrentInstanceViewportSettingsAsync()
    {
        var viewport = core.GetCurrentInstanceViewportSettings();
        return Task.FromResult(new BrowserInstanceViewportSettingsResponseDto(viewport.Width, viewport.Height, viewport.ViewportType));
    }

    /// <summary>
    /// 获取指定插件拥有的实例 ID 列表。<br/>
    /// Get the list of instance identifiers owned by the specified plugin.
    /// </summary>
    /// <param name="pluginId">插件 ID。<br/>Plugin id.</param>
    /// <returns>实例 ID 列表。<br/>List of instance identifiers.</returns>
    public IReadOnlyList<string> GetPluginInstanceIds(string pluginId) => core.GetPluginInstanceIds(pluginId);

    /// <summary>
    /// 为实例分配稳定的展示颜色。<br/>
    /// Assign a stable display color for an instance.
    /// </summary>
    /// <param name="instanceId">实例 ID。<br/>Instance id.</param>
    /// <returns>十六进制颜色值。<br/>Hex color value.</returns>
    public string GetInstanceColor(string instanceId)
    {
        var colors = new[]
        {
            "#2563eb",
            "#059669",
            "#d97706",
            "#dc2626",
            "#7c3aed",
            "#0891b2",
            "#4f46e5",
            "#db2777"
        };

        unchecked
        {
            var hash = 17;
            foreach (var ch in instanceId ?? string.Empty) hash = (hash * 31) + ch;
            return colors[Math.Abs(hash) % colors.Length];
        }
    }

    /// <summary>
    /// 获取指定实例的浏览器上下文。<br/>
    /// Get the browser context for the specified instance.
    /// </summary>
    public IBrowserContext? GetBrowserContext(string instanceId) => core.GetBrowserContext(instanceId);

    /// <summary>
    /// 获取指定实例的活动页面。<br/>
    /// Get the active page for the specified instance.
    /// </summary>
    public IPage? GetActivePage(string instanceId) => core.GetActivePage(instanceId);

    /// <summary>
    /// 创建新的浏览器实例。<br/>
    /// Create a new browser instance.
    /// </summary>
    /// <param name="ownerPluginId">实例所有者插件 ID。<br/>Owning plugin id.</param>
    /// <param name="displayName">实例显示名。<br/>Instance display name.</param>
    /// <param name="userDataDirectory">用户数据目录。<br/>User data directory.</param>
    /// <param name="previewInstanceId">预览阶段生成的实例 ID。<br/>Preview-generated instance id.</param>
    /// <param name="cancellationToken">请求取消令牌。<br/>Request cancellation token.</param>
    /// <returns>新实例 ID。<br/>New instance identifier.</returns>
    public async Task<string> CreateBrowserInstanceAsync(string ownerPluginId, string? displayName = null, string? userDataDirectory = null, string? previewInstanceId = null, CancellationToken cancellationToken = default) => await core.CreateBrowserInstanceAsync(ownerPluginId, displayName, userDataDirectory, previewInstanceId);

    /// <summary>
    /// 删除指定实例。<br/>
    /// Remove the specified instance.
    /// </summary>
    public Task<bool> RemoveBrowserInstanceAsync(string instanceId, CancellationToken cancellationToken = default) => core.RemoveAsync(instanceId);

    /// <summary>
    /// 选择指定实例为当前实例。<br/>
    /// Select the specified instance as the current instance.
    /// </summary>
    public Task<bool> SelectBrowserInstanceAsync(string instanceId, CancellationToken cancellationToken = default) => core.SelectBrowserInstanceAsync(instanceId, cancellationToken);

    /// <summary>
    /// 预览新实例的实例 ID 和目录路径。<br/>
    /// Preview the instance id and directory path for a new instance.
    /// </summary>
    public Task<(string InstanceId, string UserDataDirectory)> PreviewNewInstanceAsync(string? ownerPluginId, string? userDataDirectoryRoot) => core.PreviewNewInstanceAsync(ownerPluginId ?? "ui", userDataDirectoryRoot);

    /// <summary>
    /// 获取所有标签页的 API 视图。<br/>
    /// Get the API view of all tabs.
    /// </summary>
    /// <returns>标签页 DTO 列表。<br/>List of tab DTOs.</returns>
    public async Task<IReadOnlyList<Models.BrowserTabInfoDto>> GetTabsAsync()
    {
        var tabs = await core.GetTabsAsync();
        var selectedInstanceId = core.CurrentInstanceId;

        return [.. tabs.Select(tab =>
        {
            var instance = core.Instances.FirstOrDefault(i => i.TabIds.Contains(tab.Id));
            return new Models.BrowserTabInfoDto(
                tab.Id,
                tab.Title,
                tab.Url,
                tab.IsActive,
                tab.OwnerPluginId,
                instance?.InstanceId,
                instance?.DisplayName,
                instance is null ? null : GetInstanceColor(instance.InstanceId),
                string.Equals(instance?.InstanceId, selectedInstanceId, StringComparison.OrdinalIgnoreCase));
        })];
    }

    /// <summary>
    /// 选择指定页面。<br/>
    /// Select the specified page.
    /// </summary>
    public Task<bool> SelectPageAsync(string tabId) => core.SelectPageAsync(tabId);

    /// <summary>
    /// 关闭指定标签页。<br/>
    /// Close the specified tab.
    /// </summary>
    public Task<bool> CloseTabAsync(string tabId) => core.CloseTabAsync(tabId);

    /// <summary>
    /// 关闭指定浏览器实例。<br/>
    /// Close the specified browser instance.
    /// </summary>
    public async Task<bool> CloseBrowserInstanceAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(instanceId)) return false;
        return await core.CloseBrowserInstanceAsync(instanceId, cancellationToken);
    }

    /// <summary>
    /// 在当前实例中创建新标签页。<br/>
    /// Create a new tab in the current instance.
    /// </summary>
    public Task CreateTabAsync(string? url = null) => core.CreateTabAsync(url);

    /// <summary>
    /// 获取当前页面标题。<br/>
    /// Get the title of the current page.
    /// </summary>
    public Task<string?> GetTitleAsync() => core.GetTitleAsync();

    /// <summary>
    /// 导航到指定 URL。<br/>
    /// Navigate to the specified URL.
    /// </summary>
    public Task NavigateAsync(string url) => core.NavigateAsync(url);

    /// <summary>
    /// 在当前历史记录中后退。<br/>
    /// Navigate backward in the current history stack.
    /// </summary>
    public Task GoBackAsync() => core.GoBackAsync();

    /// <summary>
    /// 在当前历史记录中前进。<br/>
    /// Navigate forward in the current history stack.
    /// </summary>
    public Task GoForwardAsync() => core.GoForwardAsync();

    /// <summary>
    /// 刷新当前页面。<br/>
    /// Reload the current page.
    /// </summary>
    public Task ReloadAsync() => core.ReloadAsync();

    /// <summary>
    /// 捕获当前页面截图。<br/>
    /// Capture a screenshot of the current page.
    /// </summary>
    public Task<byte[]?> CaptureScreenshotAsync() => core.CaptureScreenshotAsync();

    /// <summary>
    /// 设置当前实例视口大小。<br/>
    /// Set the viewport size of the current instance.
    /// </summary>
    public Task SetViewportSizeAsync(int width, int height) => core.SetViewportSizeAsync(width, height);

    /// <summary>
    /// 更新浏览器启动设置并按需重新加载实例。<br/>
    /// Update browser launch settings and reload instances when necessary.
    /// </summary>
    public async Task UpdateLaunchSettingsAsync(string primaryUserDataDirectory, bool isHeadless, bool forceReload = false)
    {
        await core.UpdateLaunchSettingsAsync(primaryUserDataDirectory, isHeadless, forceReload);
        try
        {
            var pluginHost = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetTypes())
                .SelectMany(t => t)
                .FirstOrDefault(t => typeof(IPluginHostCore).IsAssignableFrom(t));
        }
        catch { }
    }

    /// <summary>
    /// 更新实例设置。<br/>
    /// Update instance settings.
    /// </summary>
    public Task<bool> UpdateInstanceSettingsAsync(Core.Models.BrowserInstanceSettingsUpdateRequest request, CancellationToken cancellationToken = default) => core.UpdateInstanceSettingsAsync(request, cancellationToken);

    /// <summary>
    /// 同步当前实例视口大小。<br/>
    /// Synchronize the viewport size of the current instance.
    /// </summary>
    public Task SyncCurrentInstanceViewportAsync(int width, int height, CancellationToken cancellationToken = default) => core.SyncCurrentInstanceViewportAsync(width, height, cancellationToken);
}
