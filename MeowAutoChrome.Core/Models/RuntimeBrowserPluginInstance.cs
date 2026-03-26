using MeowAutoChrome.Contracts;
using System.Threading;

namespace MeowAutoChrome.Core.Models;

public sealed class RuntimeBrowserPluginInstance
{
    public Type Type { get; }
    public IPlugin Instance { get; }
    public SemaphoreSlim ExecutionLock { get; } = new(1,1);

    private CancellationTokenSource? _lifecycleCts;

    public RuntimeBrowserPluginInstance(Type type, IPlugin instance)
    {
        Type = type;
        Instance = instance;
    }

    public CancellationToken LifecycleCancellationToken => _lifecycleCts?.Token ?? CancellationToken.None;

    public void EnsureFreshLifecycleToken()
    {
        _lifecycleCts?.Dispose();
        _lifecycleCts = new CancellationTokenSource();
    }

    public void CancelLifecycle() => _lifecycleCts?.Cancel();
}
