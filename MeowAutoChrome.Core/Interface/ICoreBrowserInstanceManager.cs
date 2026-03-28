using Microsoft.Playwright;

namespace MeowAutoChrome.Core.Interface;

/// <summary>
/// Core-internal browser instance manager abstraction.
/// Use Core-prefixed name to avoid conflicts with Contracts.
/// </summary>
public interface ICoreBrowserInstanceManager
{
    IReadOnlyCollection<ICoreBrowserInstance> Instances { get; }
    string CurrentInstanceId { get; }
    bool IsHeadless { get; }

    IBrowserContext? BrowserContext { get; }
    IPage? ActivePage { get; }
    string? SelectedPageId { get; }

    bool TryGet(string id, out ICoreBrowserInstance inst);
    Task<string> CreateAsync(string ownerId, string displayName, string userDataDir, bool headless = true, string? previewInstanceId = null);
    Task<(string InstanceId, string UserDataDirectory)> PreviewNewInstanceAsync(string ownerId, string? userDataDirRoot = null);
    IReadOnlyList<string> GetPluginInstanceIds(string pluginId);
    ICoreBrowserInstance? GetInstance(string instanceId);
    Task<bool> CloseInstanceAsync(string instanceId);
    Task UpdateLaunchSettingsAsync(string primaryUserDataDirectory, bool isHeadless, bool forceReload = false);
}
