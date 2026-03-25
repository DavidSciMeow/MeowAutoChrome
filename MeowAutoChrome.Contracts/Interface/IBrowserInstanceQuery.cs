using System.Collections.Generic;
using System.Threading.Tasks;
using MeowAutoChrome.Contracts.BrowserContext;
using Microsoft.Playwright;

namespace MeowAutoChrome.Contracts.Interface;

/// <summary>
/// Read-only query surface for browser instances.
/// Extracted from IBrowserInstanceManager to reduce interface size.
/// </summary>
public interface IBrowserInstanceQuery
{
    string CurrentInstanceId { get; }
    IReadOnlyList<BrowserInstanceInfo> GetInstances();
    IReadOnlyList<string> GetPluginInstanceIds(string pluginId);
    string? GetInstanceColor(string instanceId);
    IBrowserContext? GetBrowserContext(string instanceId);
    IPage? GetActivePage(string instanceId);
    Task<IReadOnlyList<BrowserTabInfo>> GetTabsAsync();
    BrowserInstanceViewportSettingsResponse GetCurrentInstanceViewportSettings();
    string? CurrentUrl { get; }
    int TotalPageCount { get; }
    Task<BrowserInstanceSettingsResponse?> GetInstanceSettingsAsync(string instanceId);
}
