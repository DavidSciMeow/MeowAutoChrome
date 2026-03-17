using MeowAutoChrome.Contracts.BrowserContext;
using MeowAutoChrome.Contracts.Interface;
using MeowAutoChrome.Web.Models;
using MeowAutoChrome.Web.ProgarmControl;
using MeowAutoChrome.Web.Warpper;
using Microsoft.Playwright;

namespace MeowAutoChrome.Web.Services;

/// <summary>
/// 管理多个浏览器实例（封装 Playwright 的不同 user-data-dir 与配置），
/// 提供实例的创建、移除、选择以及对标签页和视口的操作接口。
/// </summary>
public sealed class BrowserInstanceManager : IBrowserInstanceManager
{
    private const string PrimaryInstanceId = "default";
    private const string PrimaryInstanceName = "主实例";
    private static readonly HashSet<string> ReservedFileNames =
    [
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    ];
    private static readonly string[] InstanceColors =
    [
        "#2563eb",
        "#db2777",
        "#16a34a",
        "#d97706",
        "#7c3aed",
        "#0891b2",
        "#dc2626",
        "#4f46e5"
    ];

    private readonly Dictionary<string, PlayWrightWarpper> _instances = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _syncRoot = new();
    private readonly ProgramSettingsService _programSettingsService;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<BrowserInstanceManager> _logger;
    private int _colorIndex = 1;
    private string _selectedInstanceId = PrimaryInstanceId;

    /// <summary>
    /// 构造函数，初始化主实例并注入所需的服务。
    /// </summary>
    /// <param name="programSettingsService">程序设置服务。</param>
    /// <param name="loggerFactory">日志工厂。</param>
    /// <param name="logger">日志记录器。</param>
    public BrowserInstanceManager(ProgramSettingsService programSettingsService, ILoggerFactory loggerFactory, ILogger<BrowserInstanceManager> logger)
    {
        _programSettingsService = programSettingsService;
        _loggerFactory = loggerFactory;
        _logger = logger;

        _instances[PrimaryInstanceId] = CreateWrapper(PrimaryInstanceId, PrimaryInstanceName, InstanceColors[0], ownerPluginId: null, userDataDirectory: null);
    }

    /// <summary>
    /// 当前选中的浏览器实例 ID。
    /// </summary>
    public string CurrentInstanceId
    {
        get
        {
            lock (_syncRoot)
            {
                if (_instances.ContainsKey(_selectedInstanceId))
                    return _selectedInstanceId;

                _selectedInstanceId = PrimaryInstanceId;
                return _selectedInstanceId;
            }
        }
    }

    /// <summary>
    /// 主浏览器实例。
    /// </summary>
    public PlayWrightWarpper PrimaryInstance => GetRequiredInstance(PrimaryInstanceId);

    /// <summary>
    /// 当前选中的浏览器实例。
    /// </summary>
    public PlayWrightWarpper CurrentInstance => GetRequiredInstance(CurrentInstanceId);

    /// <summary>
    /// 当前实例的浏览器上下文。
    /// </summary>
    public IBrowserContext BrowserContext => CurrentInstance.BrowserContext;

    /// <summary>
    /// 当前实例的活动页面。
    /// </summary>
    public IPage? ActivePage => CurrentInstance.ActivePage;

    /// <summary>
    /// 当前实例选中的页面 ID。
    /// </summary>
    public string? SelectedPageId => CurrentInstance.SelectedPageId;

    /// <summary>
    /// 当前实例是否为无头模式。
    /// </summary>
    public bool IsHeadless => CurrentInstance.IsHeadless;

    /// <summary>
    /// 当前实例活动页面的 URL。
    /// </summary>
    public string? CurrentUrl => CurrentInstance.CurrentUrl;

    /// <summary>
    /// 所有实例的页面总数。
    /// </summary>
    public int TotalPageCount => SnapshotInstances().Sum(instance => instance.Pages.Count);

    /// <summary>
    /// 获取所有浏览器实例的概要信息。
    /// </summary>
    /// <returns>实例信息集合。</returns>
    public IReadOnlyList<BrowserInstanceInfo> GetInstances()
    {
        var currentInstanceId = CurrentInstanceId;
        return [.. SnapshotInstances()
            .Select(instance => new BrowserInstanceInfo(
                instance.InstanceId,
                instance.DisplayName,
                instance.OwnerPluginId,
                instance.Color,
                string.Equals(instance.InstanceId, currentInstanceId, StringComparison.OrdinalIgnoreCase),
                instance.Pages.Count))];
    }

    /// <summary>
    /// 获取指定实例的详细设置。
    /// </summary>
    /// <param name="instanceId">实例 ID。</param>
    /// <returns>实例设置；若实例不存在则返回 null。</returns>
    public async Task<BrowserInstanceSettingsResponse?> GetInstanceSettingsAsync(string instanceId)
    {
        if (!TryGetInstance(instanceId, out var instance))
            return null;

        var programSettings = await _programSettingsService.GetAsync();
        var programUserAgent = NormalizeOptionalValue(programSettings.UserAgent);
        var isUserAgentLocked = !string.IsNullOrWhiteSpace(programUserAgent) && !programSettings.AllowInstanceUserAgentOverride;
        var effectiveUserAgent = ResolveEffectiveUserAgent(programUserAgent, programSettings.AllowInstanceUserAgentOverride, instance.UseProgramUserAgent, instance.CustomUserAgent);

        return new BrowserInstanceSettingsResponse(
            instance.InstanceId,
            instance.DisplayName,
            instance.UserDataDirectoryPath,
            string.Equals(instance.InstanceId, CurrentInstanceId, StringComparison.OrdinalIgnoreCase),
            instance.GetViewportSettings(),
            new BrowserInstanceUserAgentSettingsResponse(
                programUserAgent,
                programSettings.AllowInstanceUserAgentOverride,
                isUserAgentLocked || instance.UseProgramUserAgent,
                instance.CustomUserAgent,
                effectiveUserAgent,
                isUserAgentLocked));
    }

    /// <summary>
    /// 获取当前实例的视口设置。
    /// </summary>
    /// <returns>视口设置响应对象。</returns>
    public BrowserInstanceViewportSettingsResponse GetCurrentInstanceViewportSettings()
        => CurrentInstance.GetViewportSettings();

    /// <summary>
    /// 获取指定插件创建的所有实例 ID。
    /// </summary>
    /// <param name="pluginId">插件 ID。</param>
    /// <returns>实例 ID 集合。</returns>
    public IReadOnlyList<string> GetPluginInstanceIds(string pluginId)
        => [.. SnapshotInstances()
            .Where(instance => string.Equals(instance.OwnerPluginId, pluginId, StringComparison.OrdinalIgnoreCase))
            .Select(instance => instance.InstanceId)];

    /// <summary>
    /// 获取指定实例的颜色。
    /// </summary>
    /// <param name="instanceId">实例 ID。</param>
    /// <returns>实例颜色；若实例不存在则返回 null。</returns>
    public string? GetInstanceColor(string instanceId)
        => TryGetInstance(instanceId, out var instance) ? instance.Color : null;

    /// <summary>
    /// 获取指定实例的浏览器上下文。
    /// </summary>
    /// <param name="instanceId">实例 ID。</param>
    /// <returns>浏览器上下文；若实例不存在则返回 null。</returns>
    public IBrowserContext? GetBrowserContext(string instanceId)
        => TryGetInstance(instanceId, out var instance) ? instance.BrowserContext : null;

    /// <summary>
    /// 获取指定实例的活动页面。
    /// </summary>
    /// <param name="instanceId">实例 ID。</param>
    /// <returns>活动页面；若实例不存在则返回 null。</returns>
    public IPage? GetActivePage(string instanceId)
        => TryGetInstance(instanceId, out var instance) ? instance.ActivePage : null;

    /// <summary>
    /// 创建一个新的浏览器实例。
    /// </summary>
    /// <param name="ownerPluginId">所属插件 ID。</param>
    /// <param name="displayName">可选实例显示名。</param>
    /// <param name="userDataDirectory">可选用户数据目录。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>新创建的实例 ID。</returns>
    public async Task<string> CreateBrowserInstanceAsync(string ownerPluginId, string? displayName = null, string? userDataDirectory = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(ownerPluginId))
            throw new InvalidOperationException("ownerPluginId 不能为空。");

        var instanceId = $"{SanitizePathSegment(ownerPluginId)}-{Guid.NewGuid():N}";
        var color = GetNextColor();
        var instanceName = string.IsNullOrWhiteSpace(displayName)
            ? BuildDefaultInstanceName(ownerPluginId)
            : displayName.Trim();
        var resolvedUserDataDirectory = string.IsNullOrWhiteSpace(userDataDirectory)
            ? ResolveDefaultUserDataDirectory(ownerPluginId, instanceName)
            : Path.GetFullPath(userDataDirectory.Trim());

        if (IsUserDataDirectoryInUse(resolvedUserDataDirectory))
            throw new InvalidOperationException($"实例 {instanceName} 对应的用户数据目录已被占用，请先关闭同名实例或改用其他实例名。");

        var instance = CreateWrapper(instanceId, instanceName, color, ownerPluginId, resolvedUserDataDirectory);

        lock (_syncRoot)
        {
            _instances[instanceId] = instance;
        }

        _logger.LogInformation("已创建浏览器实例。InstanceId: {InstanceId}; OwnerPluginId: {OwnerPluginId}; DisplayName: {DisplayName}; UserDataDirectory: {UserDataDirectory}; Color: {Color}", instanceId, ownerPluginId, instanceName, resolvedUserDataDirectory, color);
        return instanceId;
    }

    /// <summary>
    /// 移除指定浏览器实例。
    /// </summary>
    /// <param name="instanceId">实例 ID。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>是否移除成功。</returns>
    public async Task<bool> RemoveBrowserInstanceAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(instanceId) || string.Equals(instanceId, PrimaryInstanceId, StringComparison.OrdinalIgnoreCase))
            return false;

        PlayWrightWarpper? instance;
        lock (_syncRoot)
        {
            if (!_instances.Remove(instanceId, out instance))
                return false;

            if (string.Equals(_selectedInstanceId, instanceId, StringComparison.OrdinalIgnoreCase))
                _selectedInstanceId = PrimaryInstanceId;
        }

        if (instance is null)
            return false;

        await instance.CloseAsync();
        _logger.LogInformation("已移除浏览器实例。InstanceId: {InstanceId}", instanceId);
        return true;
    }

    /// <summary>
    /// 选择指定浏览器实例为当前实例。
    /// </summary>
    /// <param name="instanceId">实例 ID。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>是否选择成功。</returns>
    public async Task<bool> SelectBrowserInstanceAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!TryGetInstance(instanceId, out var instance))
            return false;

        lock (_syncRoot)
            _selectedInstanceId = instanceId;

        _ = instance.ActivePage;
        var selectedPageId = instance.SelectedPageId;
        if (!string.IsNullOrWhiteSpace(selectedPageId))
            return await instance.SelectPageAsync(selectedPageId);

        return true;
    }

    /// <summary>
    /// 获取所有实例的标签页信息。
    /// </summary>
    /// <returns>标签页信息集合。</returns>
    public async Task<IReadOnlyList<BrowserTabInfo>> GetTabsAsync()
    {
        var currentInstanceId = CurrentInstanceId;
        var orderedInstances = SnapshotInstances()
            .OrderByDescending(instance => string.Equals(instance.InstanceId, currentInstanceId, StringComparison.OrdinalIgnoreCase))
            .ThenBy(instance => instance.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var tabs = new List<BrowserTabInfo>();
        foreach (var instance in orderedInstances)
        {
            var instanceSelected = string.Equals(instance.InstanceId, currentInstanceId, StringComparison.OrdinalIgnoreCase);
            var instanceTabs = await instance.GetTabsAsync();
            tabs.AddRange(instanceTabs.Select(tab => tab with
            {
                IsSelected = instanceSelected && tab.IsSelected,
                IsInSelectedInstance = instanceSelected
            }));
        }

        return tabs;
    }

    /// <summary>
    /// 选择指定标签页并切换到对应实例。
    /// </summary>
    /// <param name="tabId">标签页 ID。</param>
    /// <returns>是否选择成功。</returns>
    public async Task<bool> SelectPageAsync(string tabId)
    {
        var instance = FindInstanceByTabId(tabId);
        if (instance is null)
            return false;

        lock (_syncRoot)
            _selectedInstanceId = instance.InstanceId;

        return await instance.SelectPageAsync(tabId);
    }

    /// <summary>
    /// 关闭指定标签页。
    /// </summary>
    /// <param name="tabId">标签页 ID。</param>
    /// <returns>是否关闭成功。</returns>
    public async Task<bool> CloseTabAsync(string tabId)
    {
        var instance = FindInstanceByTabId(tabId);
        if (instance is null)
            return false;

        var closed = await instance.CloseTabAsync(tabId);
        if (!closed)
            return false;

        if (string.Equals(instance.InstanceId, CurrentInstanceId, StringComparison.OrdinalIgnoreCase)
            && instance.Pages.Count == 0
            && !string.Equals(instance.InstanceId, PrimaryInstanceId, StringComparison.OrdinalIgnoreCase))
        {
            await SelectBrowserInstanceAsync(PrimaryInstanceId);
        }

        return true;
    }

    /// <summary>
    /// 关闭指定浏览器实例（主实例仅关闭全部标签页）。
    /// </summary>
    /// <param name="instanceId">实例 ID。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>是否关闭成功。</returns>
    public async Task<bool> CloseBrowserInstanceAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(instanceId))
            return false;

        if (string.Equals(instanceId, PrimaryInstanceId, StringComparison.OrdinalIgnoreCase))
        {
            var closed = await PrimaryInstance.CloseAllTabsAsync();
            if (closed)
            {
                lock (_syncRoot)
                    _selectedInstanceId = PrimaryInstanceId;
            }

            return closed;
        }

        return await RemoveBrowserInstanceAsync(instanceId, cancellationToken);
    }

    /// <summary>
    /// 在当前实例新建标签页。
    /// </summary>
    /// <param name="url">可选初始 URL。</param>
    /// <returns>表示异步操作的任务。</returns>
    public Task CreateTabAsync(string? url = null)
        => CurrentInstance.CreateTabAsync(url);

    /// <summary>
    /// 获取当前实例活动页标题。
    /// </summary>
    /// <returns>页面标题；若不可用则返回 null。</returns>
    public Task<string?> GetTitleAsync()
        => CurrentInstance.GetTitleAsync();

    /// <summary>
    /// 导航当前实例活动页到指定地址。
    /// </summary>
    /// <param name="url">目标地址或搜索词。</param>
    /// <returns>表示异步导航的任务。</returns>
    public Task NavigateAsync(string url)
        => CurrentInstance.NavigateAsync(url);

    /// <summary>
    /// 在当前实例活动页后退。
    /// </summary>
    /// <returns>是否后退成功。</returns>
    public Task<bool> GoBackAsync()
        => CurrentInstance.GoBackAsync();

    /// <summary>
    /// 在当前实例活动页前进。
    /// </summary>
    /// <returns>是否前进成功。</returns>
    public Task<bool> GoForwardAsync()
        => CurrentInstance.GoForwardAsync();

    /// <summary>
    /// 刷新当前实例活动页。
    /// </summary>
    /// <returns>表示异步刷新操作的任务。</returns>
    public Task ReloadAsync()
        => CurrentInstance.ReloadAsync();

    /// <summary>
    /// 截取当前实例活动页截图。
    /// </summary>
    /// <returns>截图字节数组；若无活动页则返回 null。</returns>
    public Task<byte[]?> CaptureScreenshotAsync()
        => CurrentInstance.CaptureScreenshotAsync();

    /// <summary>
    /// 设置所有实例的视口尺寸。
    /// </summary>
    /// <param name="width">目标宽度。</param>
    /// <param name="height">目标高度。</param>
    /// <returns>表示异步操作的任务。</returns>
    public async Task SetViewportSizeAsync(int width, int height)
    {
        foreach (var instance in SnapshotInstances())
            await instance.SetViewportSizeAsync(width, height);
    }

    /// <summary>
    /// 更新所有实例的启动设置。
    /// </summary>
    /// <param name="primaryUserDataDirectory">主实例用户数据目录。</param>
    /// <param name="isHeadless">是否无头模式。</param>
    /// <param name="forceReload">是否强制重载。</param>
    /// <returns>表示异步更新操作的任务。</returns>
    public async Task UpdateLaunchSettingsAsync(string primaryUserDataDirectory, bool isHeadless, bool forceReload = false)
    {
        foreach (var instance in SnapshotInstances())
        {
            var targetUserDataDirectory = string.Equals(instance.InstanceId, PrimaryInstanceId, StringComparison.OrdinalIgnoreCase)
                ? primaryUserDataDirectory
                : instance.UserDataDirectoryPath;

            await instance.UpdateLaunchSettingsAsync(targetUserDataDirectory, isHeadless, forceReload: forceReload);
        }
    }

    /// <summary>
    /// 更新指定实例的设置。
    /// </summary>
    /// <param name="instanceId">实例 ID。</param>
    /// <param name="userDataDirectory">用户数据目录。</param>
    /// <param name="viewportWidth">视口宽度。</param>
    /// <param name="viewportHeight">视口高度。</param>
    /// <param name="autoResizeViewport">是否自动调整视口。</param>
    /// <param name="preserveAspectRatio">是否保持宽高比。</param>
    /// <param name="useProgramUserAgent">是否使用程序级 User-Agent。</param>
    /// <param name="userAgent">实例自定义 User-Agent。</param>
    /// <param name="migrateExistingUserData">是否迁移现有用户数据。</param>
    /// <param name="displayWidth">可选显示宽度。</param>
    /// <param name="displayHeight">可选显示高度。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>是否更新成功。</returns>
    public async Task<bool> UpdateInstanceSettingsAsync(string instanceId, string userDataDirectory, int viewportWidth, int viewportHeight, bool autoResizeViewport, bool preserveAspectRatio, bool useProgramUserAgent, string? userAgent, bool migrateExistingUserData, int? displayWidth = null, int? displayHeight = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!TryGetInstance(instanceId, out var instance))
            return false;

        var programSettings = await _programSettingsService.GetAsync();
        var programUserAgent = NormalizeOptionalValue(programSettings.UserAgent);
        if (!string.IsNullOrWhiteSpace(programUserAgent)
            && !programSettings.AllowInstanceUserAgentOverride
            && (!useProgramUserAgent || !string.IsNullOrWhiteSpace(userAgent)))
        {
            throw new InvalidOperationException("程序设置已强制指定 User-Agent，当前不允许实例覆盖。");
        }

        var resolvedUserDataDirectory = Path.GetFullPath(userDataDirectory.Trim());
        if (IsUserDataDirectoryInUse(resolvedUserDataDirectory, instanceId))
            throw new InvalidOperationException($"实例 {instance.DisplayName} 对应的用户数据目录已被占用，请改用其他目录。");

        await instance.UpdateInstanceSettingsAsync(
            resolvedUserDataDirectory,
            instance.IsHeadless,
            viewportWidth,
            viewportHeight,
            autoResizeViewport,
            preserveAspectRatio,
            useProgramUserAgent,
            userAgent,
            migrateExistingUserData,
            displayWidth,
            displayHeight);

        if (string.Equals(instance.InstanceId, PrimaryInstanceId, StringComparison.OrdinalIgnoreCase))
        {
            var primaryProgramSettings = await _programSettingsService.GetAsync();
            primaryProgramSettings.UserDataDirectory = resolvedUserDataDirectory;
            await _programSettingsService.SaveAsync(primaryProgramSettings);
        }

        return true;
    }

    /// <summary>
    /// 同步当前实例显示视口大小。
    /// </summary>
    /// <param name="width">显示宽度。</param>
    /// <param name="height">显示高度。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示异步同步操作的任务。</returns>
    public async Task SyncCurrentInstanceViewportAsync(int width, int height, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await CurrentInstance.ApplyDisplayViewportSizeAsync(width, height);
    }

    private PlayWrightWarpper CreateWrapper(string instanceId, string displayName, string color, string? ownerPluginId, string? userDataDirectory)
        => new(_programSettingsService, _loggerFactory.CreateLogger<PlayWrightWarpper>(), instanceId, displayName, color, ownerPluginId, userDataDirectory);

    private PlayWrightWarpper GetRequiredInstance(string instanceId)
    {
        if (TryGetInstance(instanceId, out var instance))
            return instance;

        throw new InvalidOperationException($"浏览器实例不存在：{instanceId}");
    }

    private bool TryGetInstance(string instanceId, out PlayWrightWarpper instance)
    {
        lock (_syncRoot)
            return _instances.TryGetValue(instanceId, out instance!);
    }

    private IReadOnlyList<PlayWrightWarpper> SnapshotInstances()
    {
        lock (_syncRoot)
            return [.. _instances.Values];
    }

    private static string BuildDefaultInstanceName(string ownerPluginId)
        => $"{SanitizeProfileName(ownerPluginId)}-{Guid.NewGuid():N[..8]}";

    private static string ResolveDefaultUserDataDirectory(string ownerPluginId, string instanceName)
        => Path.Combine(
            ProgramSettings.GetAppDataDirectoryPath(),
            "instances",
            SanitizePathSegment(ownerPluginId),
            "profiles",
            SanitizeProfileName(instanceName));

    private static string? NormalizeOptionalValue(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? ResolveEffectiveUserAgent(string? programUserAgent, bool allowInstanceOverride, bool useProgramUserAgent, string? customUserAgent)
    {
        var normalizedProgramUserAgent = NormalizeOptionalValue(programUserAgent);
        var normalizedCustomUserAgent = NormalizeOptionalValue(customUserAgent);

        if (!string.IsNullOrWhiteSpace(normalizedProgramUserAgent))
        {
            if (allowInstanceOverride && !useProgramUserAgent && !string.IsNullOrWhiteSpace(normalizedCustomUserAgent))
                return normalizedCustomUserAgent;

            return normalizedProgramUserAgent;
        }

        return !useProgramUserAgent && !string.IsNullOrWhiteSpace(normalizedCustomUserAgent)
            ? normalizedCustomUserAgent
            : null;
    }

    private bool IsUserDataDirectoryInUse(string userDataDirectory, string? excludedInstanceId = null)
    {
        var normalizedTarget = Path.GetFullPath(userDataDirectory);
        return SnapshotInstances().Any(instance =>
            !string.Equals(instance.InstanceId, excludedInstanceId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(Path.GetFullPath(instance.UserDataDirectoryPath), normalizedTarget, StringComparison.OrdinalIgnoreCase));
    }

    private PlayWrightWarpper? FindInstanceByTabId(string tabId)
        => SnapshotInstances().FirstOrDefault(instance => instance.ContainsTab(tabId));

    private string GetNextColor()
    {
        lock (_syncRoot)
        {
            var color = InstanceColors[_colorIndex % InstanceColors.Length];
            _colorIndex++;
            return color;
        }
    }

    private static string SanitizePathSegment(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string([.. value.Trim().Select(ch => invalidChars.Contains(ch) ? '_' : ch)]);
        return string.IsNullOrWhiteSpace(sanitized) ? "instance" : sanitized;
    }

    private static string SanitizeProfileName(string value)
    {
        var sanitized = new string([.. (value ?? string.Empty)
            .Trim()
            .Select(ch => char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '-')]);

        while (sanitized.Contains("--", StringComparison.Ordinal))
            sanitized = sanitized.Replace("--", "-", StringComparison.Ordinal);

        sanitized = sanitized.Trim('-', '.', ' ');

        if (string.IsNullOrWhiteSpace(sanitized))
            sanitized = "instance";

        if (ReservedFileNames.Contains(sanitized.ToUpperInvariant()))
            sanitized = "instance-" + sanitized.ToLowerInvariant();

        return sanitized;
    }
}
