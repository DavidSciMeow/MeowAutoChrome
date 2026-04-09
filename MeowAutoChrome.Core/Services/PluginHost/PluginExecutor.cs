using MeowAutoChrome.Core.Models;
using MeowAutoChrome.Contracts;
// BrowserPlugin namespace removed from Contracts; executor uses facade IPluginContext instead.

namespace MeowAutoChrome.Core.Services.PluginHost;

/// <summary>
/// 插件执行器：在正确的宿主上下文中执行插件动作，并管理每个实例的执行同步与取消处理。<br/>
/// Responsible for executing plugin actions within the proper host context and managing per-instance execution synchronization and cancellation handling.
/// 从 BrowserPluginHostCore 中提取以简化职责。<br/>
/// Extracted from BrowserPluginHostCore to simplify responsibilities.
/// </summary>
public sealed class PluginExecutor : IPluginExecutor
{
    /// <summary>
    /// 在给定运行时实例与宿主上下文中执行插件回调，自动管理执行锁和生命周期取消信号，并返回标准化结果。<br/>
    /// Execute the provided plugin callback within the specified runtime instance and host context, managing the execution lock and lifecycle cancellation signals; returns a normalized result.
    /// </summary>
    /// <param name="instance">运行时插件实例 / runtime plugin instance.</param>
    /// <param name="hostContext">注入的插件宿主上下文 / injected plugin host context.</param>
    /// <param name="execute">实际执行插件的回调 / callback that performs plugin execution.</param>
    /// <param name="cancellationToken">外部取消令牌 / external cancellation token.</param>
    /// <returns>插件执行的标准化结果 / normalized result of the plugin execution.</returns>
    public async Task<IResult> ExecuteAsync(RuntimeBrowserPluginInstance instance, IPluginContext hostContext, Func<IPlugin, Task<IResult>> execute, CancellationToken cancellationToken)
    {
        try
        {
            await instance.ExecutionLock.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Caller requested cancellation (e.g. user clicked "Stop").
            // Signal the plugin's lifecycle token so the running plugin can observe cancellation,
            // then wait for the execution lock without the caller cancellation token so we can
            // enter the plugin context and invoke its control (Stop/Pause) methods.
            instance.CancelLifecycle();

            // Wait without the external cancellation token so we won't throw here again.
            await instance.ExecutionLock.WaitAsync();
        }
        catch
        {
            // Other cancellation/exception scenarios should propagate.
            throw;
        }

        try
        {
            // Assign to plugin HostContext (Contracts facade) for backward compatibility.
            // In future revisions we'll provide a Core-hosted ICorePluginContext adapter instead.
            instance.Instance.HostContext = hostContext;

            try
            {
                return await execute((IPlugin)instance.Instance);
            }
            finally
            {
                instance.Instance.HostContext = null;
            }
        }
        finally
        {
            instance.ExecutionLock.Release();
        }
    }
}
