using MeowAutoChrome.Contracts;

namespace MeowAutoChrome.Core.Models;

/// <summary>
/// 持有插件运行时实例与其生命周期控制辅助对象。<br/>
/// Holds a runtime plugin instance along with helpers for lifecycle control.
/// </summary>
/// <param name="type">插件实现类型（反射）/ plugin implementation type (reflection).</param>
/// <param name="instance">插件实例对象 / plugin instance object.</param>
public sealed class RuntimeBrowserPluginInstance(Type type, IPlugin instance)
{
    /// <summary>
    /// 插件实现类型（反射）。<br/>
    /// Plugin implementation type (reflection).
    /// </summary>
    public Type Type { get; } = type;
    /// <summary>
    /// 插件实例对象。<br/>
    /// Plugin instance object.
    /// </summary>
    public IPlugin Instance { get; } = instance;
    /// <summary>
    /// 执行锁，用于同步插件执行。<br/>
    /// Execution lock used to synchronize plugin execution.
    /// </summary>
    public SemaphoreSlim ExecutionLock { get; } = new(1, 1);

    private CancellationTokenSource? _lifecycleCts;

    /// <summary>
    /// 插件生命周期相关的取消令牌。<br/>
    /// Cancellation token related to the plugin lifecycle.
    /// </summary>
    public CancellationToken LifecycleCancellationToken => _lifecycleCts?.Token ?? CancellationToken.None;

    /// <summary>
    /// 确保存在一个新的生命周期取消令牌（丢弃旧的）。<br/>
    /// Ensure a fresh lifecycle cancellation token (dispose the old one).
    /// </summary>
    public void EnsureFreshLifecycleToken()
    {
        _lifecycleCts?.Dispose();
        _lifecycleCts = new CancellationTokenSource();
    }

    /// <summary>
    /// 取消插件生命周期令牌以触发停止/清理流程。<br/>
    /// Cancel the lifecycle token to trigger stop/cleanup flows.
    /// </summary>
    public void CancelLifecycle() => _lifecycleCts?.Cancel();
}
