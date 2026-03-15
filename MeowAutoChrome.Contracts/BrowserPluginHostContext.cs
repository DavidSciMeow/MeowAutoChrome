using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace MeowAutoChrome.Contracts;

public sealed class BrowserPluginHostContext : IHostContext
{
    private static readonly Task CompletedTask = Task.CompletedTask;
    private readonly Func<string?, IReadOnlyDictionary<string, string?>?, bool, Task>? _publishUpdate;

    public BrowserPluginHostContext(
        IBrowserContext browserContext,
        IPage? activePage,
        string browserInstanceId,
        IBrowserInstanceManager browserInstanceManager,
        IReadOnlyDictionary<string, string?> arguments,
        string pluginId,
        string targetId,
        CancellationToken cancellationToken,
        Func<string?, IReadOnlyDictionary<string, string?>?, bool, Task>? publishUpdate = null)
    {
        BrowserContext = browserContext;
        ActivePage = activePage;
        BrowserInstanceId = browserInstanceId;
        BrowserInstanceManager = browserInstanceManager;
        Arguments = arguments;
        PluginId = pluginId;
        TargetId = targetId;
        CancellationToken = cancellationToken;
        _publishUpdate = publishUpdate;
    }

    public IBrowserContext BrowserContext { get; }

    public IPage? ActivePage { get; }

    public string BrowserInstanceId { get; }

    public IBrowserInstanceManager BrowserInstanceManager { get; }

    public IReadOnlyDictionary<string, string?> Arguments { get; }

    public string PluginId { get; }

    public string TargetId { get; }

    public CancellationToken CancellationToken { get; }

    public Task PublishUpdateAsync(string? message, IReadOnlyDictionary<string, string?>? data = null, bool openModal = true)
        => _publishUpdate?.Invoke(message, data, openModal) ?? CompletedTask;
}
