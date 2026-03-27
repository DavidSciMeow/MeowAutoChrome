using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;
using MeowAutoChrome.Core.Adapters;
using MeowAutoChrome.Core.Interface;
using MeowAutoChrome.Contracts;

namespace MeowAutoChrome.Core.Services.PluginHost;

/// <summary>
/// Core-owned implementation of the host context used when executing plugins.
/// Implements the legacy IHostContext and the lightweight facade IPluginContext so
/// plugins depending on Contracts continue to work while Core controls the concrete
/// runtime objects.
/// </summary>
public sealed class PluginHostContextCore : IPluginContext
{
    private readonly Func<string?, IReadOnlyDictionary<string, string?>?, bool, Task>? _publishUpdate;

    public PluginHostContextCore(Microsoft.Playwright.IBrowserContext browserContext, Microsoft.Playwright.IPage? activePage, string browserInstanceId, IReadOnlyDictionary<string,string?> arguments, string pluginId, string targetId, CancellationToken cancellationToken, System.Func<string?, IReadOnlyDictionary<string, string?>?, bool, Task>? publishUpdate)
    {
        _publishUpdate = publishUpdate;
        BrowserContext = browserContext;
        ActivePage = activePage;
        BrowserInstanceId = browserInstanceId;
        Arguments = arguments ?? new Dictionary<string, string?>();
        PluginId = pluginId;
        TargetId = targetId;
        CancellationToken = cancellationToken;
    }

    public IBrowserContext BrowserContext { get; }
    public IPage? ActivePage { get; }
    public string BrowserInstanceId { get; }
    public IReadOnlyDictionary<string, string?> Arguments { get; }
    public string PluginId { get; }
    public string TargetId { get; }
    public CancellationToken CancellationToken { get; }

    public Task PublishUpdateAsync(string? message, IReadOnlyDictionary<string, string?>? data = null, bool openModal = true)
        => _publishUpdate?.Invoke(message, data, openModal) ?? Task.CompletedTask;
}
