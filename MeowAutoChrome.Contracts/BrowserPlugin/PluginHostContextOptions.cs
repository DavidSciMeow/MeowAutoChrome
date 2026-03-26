using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace MeowAutoChrome.Contracts.BrowserPlugin;

public sealed class PluginHostContextOptions
{
    public IBrowserContext BrowserContext { get; init; } = null!;
    public IPage? ActivePage { get; init; }
    public string BrowserInstanceId { get; init; } = string.Empty;
    public IBrowserInstanceManager BrowserInstanceManager { get; init; } = null!;
    public IReadOnlyDictionary<string, string?> Arguments { get; init; } = new Dictionary<string, string?>();
    public string PluginId { get; init; } = string.Empty;
    public string TargetId { get; init; } = string.Empty;
    public CancellationToken CancellationToken { get; init; }
    public Func<string?, IReadOnlyDictionary<string, string?>?, bool, Task>? PublishUpdate { get; init; }
}
