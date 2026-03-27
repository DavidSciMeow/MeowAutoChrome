using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MeowAutoChrome.Core.Models;
using MeowAutoChrome.Contracts;
using MeowAutoChrome.Core;
using MeowAutoChrome.Core.Interface;
using Microsoft.Playwright;
using Microsoft.Extensions.Logging;

namespace MeowAutoChrome.Web.Services;

public class BrowserInstanceManager
{
    private readonly BrowserInstanceManagerCore _core;
    private readonly IProgramSettingsProvider _settingsProvider;
    private readonly ILogger<BrowserInstanceManager> _logger;

    public BrowserInstanceManager(BrowserInstanceManagerCore core, IProgramSettingsProvider settingsProvider, ILogger<BrowserInstanceManager> logger)
    {
        _core = core;
        _settingsProvider = settingsProvider;
        _logger = logger;
    }

    public string CurrentInstanceId
    {
        get
        {
            var first = _core.Instances.FirstOrDefault();
            return first?.InstanceId ?? string.Empty;
        }
    }

    public dynamic? CurrentInstance => _core.Instances.FirstOrDefault();

    public IBrowserContext? BrowserContext => _core.Instances.FirstOrDefault()?.BrowserContext;

    public IPage? ActivePage => BrowserContext?.Pages.FirstOrDefault();

    public string? SelectedPageId => null;

    public bool IsHeadless => _core.Instances.FirstOrDefault()?.IsHeadless ?? false;

    public string? CurrentUrl => ActivePage?.Url;

    public int TotalPageCount => _core.Instances.Sum(i => i.BrowserContext?.Pages.Count ?? 0);

    public IReadOnlyList<BrowserInstanceInfo> GetInstances()
    {
        return _core.Instances.Select(i => new BrowserInstanceInfo(i.InstanceId, i.DisplayName, null, "#000000", string.Equals(i.InstanceId, CurrentInstanceId, StringComparison.OrdinalIgnoreCase), i.BrowserContext?.Pages.Count ?? 0)).ToArray();
    }

    public async Task<BrowserInstanceSettingsResponse?> GetInstanceSettingsAsync(string instanceId)
    {
        var inst = _core.Instances.FirstOrDefault(i => i.InstanceId == instanceId);
        if (inst == null) return null;
        return new BrowserInstanceSettingsResponse(inst.InstanceId, inst.DisplayName, inst.UserDataDirectoryPath);
    }

    public MeowAutoChrome.Core.Models.BrowserInstanceViewportSettingsResponse GetCurrentInstanceViewportSettings()
    {
        return new MeowAutoChrome.Core.Models.BrowserInstanceViewportSettingsResponse(1280, 800, "Auto");
    }

    public IReadOnlyList<string> GetPluginInstanceIds(string pluginId)
        => Array.Empty<string>();

    public string? GetInstanceColor(string instanceId) => "#000000";

    public IBrowserContext? GetBrowserContext(string instanceId)
        => _core.Instances.FirstOrDefault(i => i.InstanceId == instanceId)?.BrowserContext;

    public IPage? GetActivePage(string instanceId)
        => _core.Instances.FirstOrDefault(i => i.InstanceId == instanceId)?.BrowserContext?.Pages.FirstOrDefault();

    public Task<string> CreateBrowserInstanceAsync(string ownerPluginId, string? displayName = null, string? userDataDirectory = null, string? previewInstanceId = null, CancellationToken cancellationToken = default)
    {
        var name = string.IsNullOrWhiteSpace(displayName) ? ownerPluginId : displayName;
        var dir = string.IsNullOrWhiteSpace(userDataDirectory) ? MeowAutoChrome.Core.Struct.ProgramSettings.GetDefaultUserDataDirectoryPath() : Path.GetFullPath(userDataDirectory);
        return _core.CreateAsync(ownerPluginId, name, dir, headless: true, previewInstanceId);
    }

    public Task<bool> RemoveBrowserInstanceAsync(string instanceId, CancellationToken cancellationToken = default)
        => _core.RemoveAsync(instanceId);

    public Task<bool> SelectBrowserInstanceAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        var ok = _core.TryGet(instanceId, out var inst);
        return Task.FromResult(ok);
    }

    public Task<(string InstanceId, string UserDataDirectory)> PreviewNewInstanceAsync(string? ownerPluginId, string? userDataDirectoryRoot)
        => _core.PreviewNewInstanceAsync(ownerPluginId ?? "ui", userDataDirectoryRoot);

    public async Task<IReadOnlyList<MeowAutoChrome.Core.Models.BrowserTabInfo>> GetTabsAsync()
    {
        var tabs = new List<MeowAutoChrome.Core.Models.BrowserTabInfo>();
        foreach (var inst in _core.Instances)
        {
            var pages = inst.BrowserContext?.Pages ?? new List<IPage>();
            foreach (var page in pages)
            {
                tabs.Add(new MeowAutoChrome.Core.Models.BrowserTabInfo(Guid.NewGuid().ToString("N"), await SafeTitleAsync(page), page.Url, false, null));
            }
        }

        return tabs;

        static async Task<string?> SafeTitleAsync(IPage p)
        {
            try { return await p.TitleAsync(); } catch { return null; }
        }
    }

    public Task<bool> SelectPageAsync(string tabId) => Task.FromResult(false);
    public Task<bool> CloseTabAsync(string tabId) => Task.FromResult(false);

    public async Task<bool> CloseBrowserInstanceAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(instanceId)) return false;
        return await _core.RemoveAsync(instanceId);
    }

    public Task CreateTabAsync(string? url = null) => Task.CompletedTask;
    public Task<string?> GetTitleAsync() => Task.FromResult<string?>(null);
    public Task NavigateAsync(string url) => Task.CompletedTask;
    public Task GoBackAsync() => Task.CompletedTask;
    public Task GoForwardAsync() => Task.CompletedTask;
    public Task ReloadAsync() => Task.CompletedTask;
    public Task<byte[]?> CaptureScreenshotAsync() => Task.FromResult<byte[]?>(null);
    public Task SetViewportSizeAsync(int width, int height) => Task.CompletedTask;
    public Task UpdateLaunchSettingsAsync(string primaryUserDataDirectory, bool isHeadless, bool forceReload = false) => Task.CompletedTask;
    public Task<bool> UpdateInstanceSettingsAsync(MeowAutoChrome.Core.Models.BrowserInstanceSettingsUpdateRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    // Long-parameter overload removed; callers should use BrowserInstanceSettingsUpdateRequest instead.
    public Task SyncCurrentInstanceViewportAsync(int width, int height, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
