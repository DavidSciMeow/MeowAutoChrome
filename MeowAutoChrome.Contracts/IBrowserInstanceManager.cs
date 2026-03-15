using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace MeowAutoChrome.Contracts;

public sealed record BrowserInstanceInfo(
    string Id,
    string Name,
    string? OwnerPluginId,
    string Color,
    bool IsSelected,
    int PageCount);

public interface IBrowserInstanceManager
{
    string CurrentInstanceId { get; }
    IReadOnlyList<BrowserInstanceInfo> GetInstances();
    IReadOnlyList<string> GetPluginInstanceIds(string pluginId);
    string? GetInstanceColor(string instanceId);
    IBrowserContext? GetBrowserContext(string instanceId);
    IPage? GetActivePage(string instanceId);
    Task<string> CreateBrowserInstanceAsync(string ownerPluginId, string? displayName = null, CancellationToken cancellationToken = default);
    Task<bool> RemoveBrowserInstanceAsync(string instanceId, CancellationToken cancellationToken = default);
    Task<bool> SelectBrowserInstanceAsync(string instanceId, CancellationToken cancellationToken = default);
}
