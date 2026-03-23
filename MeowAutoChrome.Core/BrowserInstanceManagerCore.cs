using Microsoft.Playwright;
using MeowAutoChrome.Core;
using MeowAutoChrome.Contracts.Interface;
using MeowAutoChrome.Contracts.BrowserContext;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Concurrent;
using System.Linq;

namespace MeowAutoChrome.Core;

public class BrowserInstanceManagerCore : IBrowserInstanceManager
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

    // Explicit interface implementations to avoid overload ambiguity with the
    // public helpers on this class.
    Task<string> IBrowserInstanceManager.CreateBrowserInstanceAsync(string ownerPluginId, string? displayName, string? userDataDirectory, CancellationToken cancellationToken)
        => CreateBrowserInstanceAsync(ownerPluginId, displayName, userDataDirectory);

    Task<bool> IBrowserInstanceManager.RemoveBrowserInstanceAsync(string instanceId, CancellationToken cancellationToken)
        => RemoveAsync(instanceId);

    Task<bool> IBrowserInstanceManager.SelectBrowserInstanceAsync(string instanceId, CancellationToken cancellationToken)
        => SelectBrowserInstanceAsync(instanceId, cancellationToken);

    public async Task<string> CreateAsync(string ownerId, string displayName, string userDataDir, bool headless = true)
    {
        var id = $"{ownerId}-{Guid.NewGuid():N}";
        var inst = new PlaywrightInstance(_loggerFactory?.CreateLogger<PlaywrightInstance>() ?? NullLogger<PlaywrightInstance>.Instance, id, displayName, ownerId);
        if (!_instances.TryAdd(id, inst))
            throw new InvalidOperationException("Failed to add instance");
        await inst.InitializeAsync(userDataDir, headless);
        _currentInstanceId = id;
        _logger.LogInformation("Created instance {Id}", id);
        return id;
    }

    // High-level helpers used by Web controllers
    public async Task<string> CreateBrowserInstanceAsync(string ownerPluginId, string? displayName = null, string? userDataDirectory = null)
    {
        var settings = _settingsProvider is null ? new ProgramSettings() : await _settingsProvider.GetAsync();
        var userData = string.IsNullOrWhiteSpace(userDataDirectory) ? settings.UserDataDirectory : userDataDirectory!;
        var name = string.IsNullOrWhiteSpace(displayName) ? "Browser" : displayName!;
        return await CreateAsync(ownerPluginId, name, userData, settings.Headless);
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

    public async Task<bool> CloseTabAsync(string tabId)
    {
        foreach (var kv in _instances)
        {
            if (kv.Value.TabIds.Contains(tabId))
                return await kv.Value.CloseTabAsync(tabId);
        }

        return false;
    }

    public async Task<bool> UpdateInstanceSettingsAsync(string instanceId, string userDataDirectory, int viewportWidth, int viewportHeight, bool autoResizeViewport, bool preserveAspectRatio, bool useProgramUserAgent, string? userAgent, bool migrateExistingUserData, int? displayWidth, int? displayHeight, CancellationToken cancellationToken = default)
    {
        if (!_instances.TryGetValue(instanceId, out var inst)) return false;
        inst.UserDataDirectoryPath = userDataDirectory;
        // Note: real migration/viewport handling omitted for brevity
        return true;
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

    public async Task<IReadOnlyList<BrowserTabInfo>> GetTabsAsync()
    {
        var tabs = new List<BrowserTabInfo>();
        foreach (var inst in _instances.Values)
        {
            foreach (var id in inst.TabIds)
            {
                var page = inst.GetPageById(id);
                tabs.Add(new BrowserTabInfo(id, page?.TitleAsync().GetAwaiter().GetResult(), page?.Url, inst.SelectedPageId == id, inst.InstanceId, inst.DisplayName, "#ccc", inst.OwnerId, inst.SelectedPageId == id));
            }
        }

        return tabs;
    }

    public BrowserInstanceViewportSettingsResponse GetCurrentInstanceViewportSettings()
    {
        return new BrowserInstanceViewportSettingsResponse(1280, 800, true, true);
    }

    public async Task<BrowserInstanceSettingsResponse?> GetInstanceSettingsAsync(string instanceId)
    {
        if (!_instances.TryGetValue(instanceId, out var inst)) return null;
        return new BrowserInstanceSettingsResponse(inst.InstanceId, inst.DisplayName, inst.UserDataDirectoryPath, inst.SelectedPageId == inst.SelectedPageId, new BrowserInstanceViewportSettingsResponse(1280,800,true,true), new BrowserInstanceUserAgentSettingsResponse(null, false, false, null, null, false));
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
