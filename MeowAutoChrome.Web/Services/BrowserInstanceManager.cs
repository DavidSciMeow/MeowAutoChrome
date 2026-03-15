using MeowAutoChrome.Contracts;
using MeowAutoChrome.Web.Models;
using MeowAutoChrome.Web.Warpper;
using Microsoft.Playwright;

namespace MeowAutoChrome.Web.Services;

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

    public BrowserInstanceManager(ProgramSettingsService programSettingsService, ILoggerFactory loggerFactory, ILogger<BrowserInstanceManager> logger)
    {
        _programSettingsService = programSettingsService;
        _loggerFactory = loggerFactory;
        _logger = logger;

        _instances[PrimaryInstanceId] = CreateWrapper(PrimaryInstanceId, PrimaryInstanceName, InstanceColors[0], ownerPluginId: null, userDataDirectory: null);
    }

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

    public PlayWrightWarpper PrimaryInstance => GetRequiredInstance(PrimaryInstanceId);

    public PlayWrightWarpper CurrentInstance => GetRequiredInstance(CurrentInstanceId);

    public IBrowserContext BrowserContext => CurrentInstance.BrowserContext;

    public IPage? ActivePage => CurrentInstance.ActivePage;

    public string? SelectedPageId => CurrentInstance.SelectedPageId;

    public bool IsHeadless => CurrentInstance.IsHeadless;

    public string? CurrentUrl => CurrentInstance.CurrentUrl;

    public int TotalPageCount => SnapshotInstances().Sum(instance => instance.Pages.Count);

    public IReadOnlyList<BrowserInstanceInfo> GetInstances()
    {
        var currentInstanceId = CurrentInstanceId;
        return SnapshotInstances()
            .Select(instance => new BrowserInstanceInfo(
                instance.InstanceId,
                instance.DisplayName,
                instance.OwnerPluginId,
                instance.Color,
                string.Equals(instance.InstanceId, currentInstanceId, StringComparison.OrdinalIgnoreCase),
                instance.Pages.Count))
            .ToArray();
    }

    public IReadOnlyList<string> GetPluginInstanceIds(string pluginId)
        => SnapshotInstances()
            .Where(instance => string.Equals(instance.OwnerPluginId, pluginId, StringComparison.OrdinalIgnoreCase))
            .Select(instance => instance.InstanceId)
            .ToArray();

    public string? GetInstanceColor(string instanceId)
        => TryGetInstance(instanceId, out var instance) ? instance.Color : null;

    public IBrowserContext? GetBrowserContext(string instanceId)
        => TryGetInstance(instanceId, out var instance) ? instance.BrowserContext : null;

    public IPage? GetActivePage(string instanceId)
        => TryGetInstance(instanceId, out var instance) ? instance.ActivePage : null;

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

    public async Task<bool> SelectPageAsync(string tabId)
    {
        var instance = FindInstanceByTabId(tabId);
        if (instance is null)
            return false;

        lock (_syncRoot)
            _selectedInstanceId = instance.InstanceId;

        return await instance.SelectPageAsync(tabId);
    }

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

    public Task CreateTabAsync(string? url = null)
        => CurrentInstance.CreateTabAsync(url);

    public Task<string?> GetTitleAsync()
        => CurrentInstance.GetTitleAsync();

    public Task NavigateAsync(string url)
        => CurrentInstance.NavigateAsync(url);

    public Task<bool> GoBackAsync()
        => CurrentInstance.GoBackAsync();

    public Task<bool> GoForwardAsync()
        => CurrentInstance.GoForwardAsync();

    public Task ReloadAsync()
        => CurrentInstance.ReloadAsync();

    public Task<byte[]?> CaptureScreenshotAsync()
        => CurrentInstance.CaptureScreenshotAsync();

    public async Task SetViewportSizeAsync(int width, int height)
    {
        foreach (var instance in SnapshotInstances())
            await instance.SetViewportSizeAsync(width, height);
    }

    public async Task UpdateLaunchSettingsAsync(string primaryUserDataDirectory, bool isHeadless)
    {
        foreach (var instance in SnapshotInstances())
        {
            var targetUserDataDirectory = string.Equals(instance.InstanceId, PrimaryInstanceId, StringComparison.OrdinalIgnoreCase)
                ? primaryUserDataDirectory
                : instance.UserDataDirectoryPath;

            await instance.UpdateLaunchSettingsAsync(targetUserDataDirectory, isHeadless);
        }
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
            return _instances.Values.ToArray();
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

    private bool IsUserDataDirectoryInUse(string userDataDirectory)
    {
        var normalizedTarget = Path.GetFullPath(userDataDirectory);
        return SnapshotInstances().Any(instance => string.Equals(Path.GetFullPath(instance.UserDataDirectoryPath), normalizedTarget, StringComparison.OrdinalIgnoreCase));
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
        var sanitized = new string(value.Trim().Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "instance" : sanitized;
    }

    private static string SanitizeProfileName(string value)
    {
        var sanitized = new string((value ?? string.Empty)
            .Trim()
            .Select(ch => char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '-')
            .ToArray());

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
