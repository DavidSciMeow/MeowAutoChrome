using Microsoft.Playwright;
using MeowAutoChrome.Core;
using MeowAutoChrome.Core.Struct;
using MeowAutoChrome.Core.Interface;
using MeowAutoChrome.Contracts.Interface;
using MeowAutoChrome.Contracts.BrowserContext;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;

namespace MeowAutoChrome.Core;

public class BrowserInstanceManagerCore : MeowAutoChrome.Contracts.IBrowserInstanceManager
{
    private readonly ILogger<BrowserInstanceManagerCore> _logger;
    private readonly ILoggerFactory? _loggerFactory;
    private readonly IProgramSettingsProvider? _settingsProvider;
    private readonly ConcurrentDictionary<string, PlaywrightInstance> _instances = new();
    private string? _currentInstanceId;

    public BrowserInstanceManagerCore(ILogger<BrowserInstanceManagerCore> logger, ILoggerFactory? loggerFactory = null, IProgramSettingsProvider? settingsProvider = null)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _settingsProvider = settingsProvider;
    }

    // Explicit interface implementations to satisfy IBrowserInstanceManager
    Task<string> MeowAutoChrome.Contracts.IBrowserInstanceManager.CreateBrowserInstanceAsync(string ownerPluginId, string? displayName, string? userDataDirectory, string? previewInstanceId, CancellationToken cancellationToken)
        => CreateBrowserInstanceAsync(ownerPluginId, displayName, userDataDirectory, previewInstanceId);

    Task<bool> MeowAutoChrome.Contracts.IBrowserInstanceManager.RemoveBrowserInstanceAsync(string instanceId, CancellationToken cancellationToken)
        => RemoveAsync(instanceId);

    Task<bool> MeowAutoChrome.Contracts.IBrowserInstanceManager.SelectBrowserInstanceAsync(string instanceId, CancellationToken cancellationToken)
        => SelectBrowserInstanceAsync(instanceId, cancellationToken);

    Task<(string InstanceId, string UserDataDirectory)> MeowAutoChrome.Contracts.IBrowserInstanceManager.PreviewNewInstanceAsync(string? ownerPluginId, string? userDataDirectoryRoot)
        => PreviewNewInstanceAsync(ownerPluginId ?? "ui", userDataDirectoryRoot);

    // PreviewNewInstanceAsync is provided as a public method for wrappers to call.

    /// <summary>
    /// Preview a new instance id and the user-data directory that would be used if created.
    /// This does not create any files or instances.
    /// </summary>
    public async Task<(string InstanceId, string UserDataDirectory)> PreviewNewInstanceAsync(string ownerId, string? userDataDirRoot = null)
    {
        var settings = _settingsProvider is null ? new MeowAutoChrome.Core.Struct.ProgramSettings() : await _settingsProvider.GetAsync();
        var root = string.IsNullOrWhiteSpace(userDataDirRoot) ? settings.UserDataDirectory : userDataDirRoot!;
        var shortId = Guid.NewGuid().ToString("N").Substring(0, 8);
        var id = $"{ownerId}-{shortId}";
        var instanceUserDataDir = Path.Combine(root, id);
        return (id, instanceUserDataDir);
    }

    public IReadOnlyCollection<PlaywrightInstance> Instances => _instances.Values.ToList().AsReadOnly();

    public PlaywrightInstance? CurrentInstance
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(_currentInstanceId) && _instances.TryGetValue(_currentInstanceId, out var inst))
                return inst;
            return _instances.Values.FirstOrDefault();
        }
    }

    public string CurrentInstanceId => !string.IsNullOrWhiteSpace(_currentInstanceId) ? _currentInstanceId : (CurrentInstance?.InstanceId ?? string.Empty);
    public bool IsHeadless => CurrentInstance?.IsHeadless ?? true;
    public IBrowserContext? BrowserContext => CurrentInstance?.BrowserContext;
    public IPage? ActivePage => CurrentInstance?.GetSelectedPage();
    public string? SelectedPageId => CurrentInstance?.SelectedPageId;
    public int TotalPageCount => _instances.Values.Sum(i => i.Pages.Count);

    public string? CurrentUrl => ActivePage?.Url;

    // IBrowserInstanceManager compatibility
    public IReadOnlyList<BrowserInstanceInfo> GetInstances() => Instances.Select(i => new BrowserInstanceInfo(i.InstanceId, i.DisplayName, i.OwnerId, "#ccc", string.Equals(i.InstanceId, CurrentInstanceId, StringComparison.OrdinalIgnoreCase), i.Pages.Count)).ToArray();

    public IReadOnlyList<string> GetPluginInstanceIds(string pluginId) => Instances.Where(i => string.Equals(i.OwnerId, pluginId, StringComparison.OrdinalIgnoreCase)).Select(i => i.InstanceId).ToArray();

    public string? GetInstanceColor(string instanceId) => "#ccc";

    public IBrowserContext? GetBrowserContext(string instanceId) => _instances.TryGetValue(instanceId, out var inst) ? inst.BrowserContext : null;

    public IPage? GetActivePage(string instanceId) => _instances.TryGetValue(instanceId, out var inst2) ? inst2.GetSelectedPage() : null;

    // Note: this core class does not implement IBrowserInstanceManager directly.
    // The Web-level adapter `MeowAutoChrome.Web.Services.BrowserInstanceManager` implements
    // IBrowserInstanceManager and delegates to this core class.

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
    public async Task<string> CreateBrowserInstanceAsync(string ownerPluginId, string? displayName = null, string? userDataDirectory = null, string? previewInstanceId = null)
    {
        var settings = _settingsProvider is null ? new MeowAutoChrome.Core.Struct.ProgramSettings() : await _settingsProvider.GetAsync();
        var userData = string.IsNullOrWhiteSpace(userDataDirectory) ? settings.UserDataDirectory : userDataDirectory!;
        var name = string.IsNullOrWhiteSpace(displayName) ? "Browser" : displayName!;
        return await CreateAsync(ownerPluginId, name, userData, settings.Headless, previewInstanceId);
    }

    public async Task CreateTabAsync(string? url = null)
    {
        if (CurrentInstance is null)
            await CreateBrowserInstanceAsync("ui");

        await CurrentInstance!.CreateTabAsync(url);
    }

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
    public Task<bool> SelectBrowserInstanceAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        if (!_instances.ContainsKey(instanceId)) return Task.FromResult(false);
        _currentInstanceId = instanceId;
        return Task.FromResult(true);
    }

    public async Task<bool> CloseBrowserInstanceAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        return await RemoveAsync(instanceId);
    }

    public Task<byte[]?> CaptureScreenshotAsync() => Task.FromResult<byte[]?>(null);

    public Task SetViewportSizeAsync(int width, int height) => Task.CompletedTask;

    public Task UpdateLaunchSettingsAsync(string primaryUserDataDirectory, bool isHeadless, bool forceReload = false) => Task.CompletedTask;

    public async Task<bool> CloseTabAsync(string tabId)
    {
        foreach (var kv in _instances)
        {
            if (kv.Value.TabIds.Contains(tabId))
                return await kv.Value.CloseTabAsync(tabId);
        }

        return false;
    }

    public async Task<bool> UpdateInstanceSettingsAsync(MeowAutoChrome.Contracts.BrowserContext.BrowserInstanceSettingsUpdateRequest request, CancellationToken cancellationToken = default)
    {
        if (!_instances.TryGetValue(request.InstanceId, out var inst)) return false;
        inst.UserDataDirectoryPath = request.UserDataDirectory;
        // Note: real migration/viewport handling omitted for brevity
        return true;
    }

    // Backward-compatible overload with many parameters. Builds DTO and delegates to new API.
    public Task<bool> UpdateInstanceSettingsAsync(string instanceId, string userDataDirectory, int viewportWidth, int viewportHeight, bool autoResizeViewport, bool preserveAspectRatio, bool useProgramUserAgent, string? userAgent, bool migrateExistingUserData, int? displayWidth = null, int? displayHeight = null, CancellationToken cancellationToken = default)
    {
        var req = new MeowAutoChrome.Contracts.BrowserContext.BrowserInstanceSettingsUpdateRequest(
            instanceId,
            userDataDirectory,
            viewportWidth,
            viewportHeight,
            autoResizeViewport,
            preserveAspectRatio,
            useProgramUserAgent,
            userAgent,
            migrateExistingUserData,
            displayWidth,
            displayHeight);

        return UpdateInstanceSettingsAsync(req, cancellationToken);
    }

    public async Task SyncCurrentInstanceViewportAsync(int width, int height, CancellationToken cancellationToken = default)
    {
        // noop for now
        await Task.CompletedTask;
    }

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

    public async Task GoBackAsync()
    {
        if (CurrentInstance?.GetSelectedPage() is IPage page) { try { await page.GoBackAsync(); } catch { } }
    }

    public async Task GoForwardAsync()
    {
        if (CurrentInstance?.GetSelectedPage() is IPage page) { try { await page.GoForwardAsync(); } catch { } }
    }

    public async Task ReloadAsync()
    {
        if (CurrentInstance?.GetSelectedPage() is IPage page) { try { await page.ReloadAsync(); } catch { } }
    }

    public async Task<string?> GetTitleAsync()
    {
        if (CurrentInstance?.GetSelectedPage() is IPage page) { try { return await page.TitleAsync(); } catch { } }
        return null;
    }

    public async Task<IReadOnlyList<MeowAutoChrome.Contracts.BrowserContext.BrowserTabInfo>> GetTabsAsync()
    {
        var tabs = new List<MeowAutoChrome.Contracts.BrowserContext.BrowserTabInfo>();
        foreach (var inst in _instances.Values)
        {
            foreach (var id in inst.TabIds)
            {
                var page = inst.GetPageById(id);
                tabs.Add(new MeowAutoChrome.Contracts.BrowserContext.BrowserTabInfo(id, page?.TitleAsync().GetAwaiter().GetResult(), page?.Url, inst.SelectedPageId == id, inst.InstanceId, inst.DisplayName, "#ccc", inst.OwnerId, inst.SelectedPageId == id));
            }
        }

        return tabs;
    }

    public MeowAutoChrome.Contracts.BrowserContext.BrowserInstanceViewportSettingsResponse GetCurrentInstanceViewportSettings()
    {
        return new MeowAutoChrome.Contracts.BrowserContext.BrowserInstanceViewportSettingsResponse(1280, 800, true, true);
    }

    public async Task<MeowAutoChrome.Contracts.BrowserContext.BrowserInstanceSettingsResponse?> GetInstanceSettingsAsync(string instanceId)
    {
        if (!_instances.TryGetValue(instanceId, out var inst)) return null;
        return new MeowAutoChrome.Contracts.BrowserContext.BrowserInstanceSettingsResponse(inst.InstanceId, inst.DisplayName, inst.UserDataDirectoryPath, inst.SelectedPageId == inst.SelectedPageId, new MeowAutoChrome.Contracts.BrowserContext.BrowserInstanceViewportSettingsResponse(1280,800,true,true), new MeowAutoChrome.Contracts.BrowserContext.BrowserInstanceUserAgentSettingsResponse(null, false, false, null, null, false));
    }

    public bool TryGet(string id, out PlaywrightInstance inst) => _instances.TryGetValue(id, out inst);

    public async Task<bool> RemoveAsync(string id)
    {
        if (!_instances.TryRemove(id, out var inst))
            return false;

        await inst.CloseAsync();
        _logger.LogInformation("Removed instance {Id}", id);
        return true;
    }
}
