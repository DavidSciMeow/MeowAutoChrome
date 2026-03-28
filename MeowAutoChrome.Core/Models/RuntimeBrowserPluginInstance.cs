using MeowAutoChrome.Contracts;

namespace MeowAutoChrome.Core.Models;

public sealed class RuntimeBrowserPluginInstance(Type type, IPlugin instance)
{
    public Type Type { get; } = type;
    public IPlugin Instance { get; } = instance;
    public SemaphoreSlim ExecutionLock { get; } = new(1,1);

    private CancellationTokenSource? _lifecycleCts;

    public CancellationToken LifecycleCancellationToken => _lifecycleCts?.Token ?? CancellationToken.None;

    public void EnsureFreshLifecycleToken()
    {
        _lifecycleCts?.Dispose();
        _lifecycleCts = new CancellationTokenSource();
    }

    public void CancelLifecycle() => _lifecycleCts?.Cancel();
}
