using System.Collections.Generic;
using System.Threading;
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
}
