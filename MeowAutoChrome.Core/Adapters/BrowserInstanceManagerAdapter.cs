using MeowAutoChrome.Core.Models;
using MeowAutoChrome.Core.Interface;
using Microsoft.Playwright;

namespace MeowAutoChrome.Core.Adapters;

public sealed class BrowserInstanceManagerAdapter
{
    private readonly ICoreBrowserInstanceManager _core;

    public BrowserInstanceManagerAdapter(ICoreBrowserInstanceManager core)
    {
        _core = core ?? throw new ArgumentNullException(nameof(core));
    }

    // Read-only query functions can be mapped
    public string CurrentInstanceId => _core.CurrentInstanceId;

    public Task CreateTabAsync(string? url = null) => throw new NotSupportedException("CreateTabAsync must be performed via Core APIs.");
    public Task<string?> GetTitleAsync() => Task.FromResult<string?>(null);
    public Task NavigateAsync(string url) => throw new NotSupportedException();
    public Task GoBackAsync() => throw new NotSupportedException();
    public Task GoForwardAsync() => throw new NotSupportedException();
    public Task ReloadAsync() => throw new NotSupportedException();
    public Task<byte[]?> CaptureScreenshotAsync() => Task.FromResult<byte[]?>(null);
    public Task SetViewportSizeAsync(int width, int height) => throw new NotSupportedException();
    public Task UpdateLaunchSettingsAsync(string primaryUserDataDirectory, bool isHeadless, bool forceReload = false) => throw new NotSupportedException();
    public Task<bool> UpdateInstanceSettingsAsync(BrowserInstanceSettingsUpdateRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task SyncCurrentInstanceViewportAsync(int width, int height, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<bool> CloseTabAsync(string tabId) => throw new NotSupportedException();
    public Task<bool> CloseBrowserInstanceAsync(string instanceId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<bool> SelectPageAsync(string tabId) => throw new NotSupportedException();
    public Task<string> CreateBrowserInstanceAsync(string ownerPluginId, string? displayName = null, string? userDataDirectory = null, string? previewInstanceId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<bool> RemoveBrowserInstanceAsync(string instanceId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<bool> SelectBrowserInstanceAsync(string instanceId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<(string InstanceId, string UserDataDirectory)> PreviewNewInstanceAsync(string? ownerPluginId, string? userDataDirectoryRoot) => throw new NotSupportedException();

    // IBrowserInstanceQuery members
    public IReadOnlyList<BrowserInstanceInfo> GetInstances() => throw new NotSupportedException();
    public IReadOnlyList<string> GetPluginInstanceIds(string pluginId) => throw new NotSupportedException();
    public string? GetInstanceColor(string instanceId) => null;
    public IBrowserContext? GetBrowserContext(string instanceId) => _core.BrowserContext;
    public IPage? GetActivePage(string instanceId) => _core.ActivePage;
    public Task<IReadOnlyList<BrowserTabInfo>> GetTabsAsync() => throw new NotSupportedException();
    public BrowserInstanceViewportSettingsResponse GetCurrentInstanceViewportSettings() => throw new NotSupportedException();
    public string? CurrentUrl => null;
    public int TotalPageCount => 0;
    public Task<BrowserInstanceSettingsResponse?> GetInstanceSettingsAsync(string instanceId) => throw new NotSupportedException();
}
