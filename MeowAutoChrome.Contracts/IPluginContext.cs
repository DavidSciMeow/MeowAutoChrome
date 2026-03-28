using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace MeowAutoChrome.Contracts;

/// <summary>
/// Minimal plugin-facing context. Plugins should only depend on this facade.
/// Contains only readonly handles and the cancellation token.
/// </summary>
public interface IPluginContext
{
    IBrowserContext BrowserContext { get; }
    IPage? ActivePage { get; }
    string PluginId { get; }
    IReadOnlyDictionary<string, string?> Arguments { get; }
    CancellationToken CancellationToken { get; }

    /// <summary>
    /// Request the host to create a new browser context / instance configured by the provided options.
    /// The host may return an IBrowserContext directly (if it can safely share it), or null on failure.
    /// Hosts are expected to enforce quotas and validate options.
    /// </summary>
    Task<string?> RequestNewBrowserInstanceAsync(BrowserCreationOptions options, CancellationToken cancellationToken = default);
    /// <summary>
    /// Request information about a browser instance known to the host.
    /// Returns null if the instance is not found or host does not expose metadata.
    /// </summary>
    Task<PluginBrowserInstanceInfo?> GetBrowserInstanceInfoAsync(string instanceId, CancellationToken cancellationToken = default);
}
