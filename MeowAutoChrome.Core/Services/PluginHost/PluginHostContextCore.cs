using Microsoft.Playwright;
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
    private readonly Func<BrowserCreationOptions, CancellationToken, Task<string?>>? _requestNewInstance;
    private readonly Func<string, CancellationToken, Task<PluginBrowserInstanceInfo?>>? _getInstanceInfo;

public PluginHostContextCore(IBrowserContext browserContext, IPage? activePage, string browserInstanceId, IReadOnlyDictionary<string,string?> arguments, string pluginId, string targetId, CancellationToken cancellationToken, Func<string?, IReadOnlyDictionary<string, string?>?, bool, Task>? publishUpdate, Func<BrowserCreationOptions, CancellationToken, Task<string?>>? requestNewInstance = null, Func<string, CancellationToken, Task<PluginBrowserInstanceInfo?>>? getInstanceInfo = null)
    {
        _publishUpdate = publishUpdate;
        _requestNewInstance = requestNewInstance;
        _getInstanceInfo = getInstanceInfo;
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

    public Task<string?> RequestNewBrowserInstanceAsync(BrowserCreationOptions options, CancellationToken cancellationToken = default)
        => _requestNewInstance is null ? Task.FromResult<string?>(null) : _requestNewInstance(options, cancellationToken);

    public Task<PluginBrowserInstanceInfo?> GetBrowserInstanceInfoAsync(string instanceId, CancellationToken cancellationToken = default)
        => _getInstanceInfo is null ? Task.FromResult<PluginBrowserInstanceInfo?>(null) : _getInstanceInfo(instanceId, cancellationToken);
}
