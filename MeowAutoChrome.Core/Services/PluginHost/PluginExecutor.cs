using MeowAutoChrome.Core.Models;
using MeowAutoChrome.Contracts;
// BrowserPlugin namespace removed from Contracts; executor uses facade IPluginContext instead.

namespace MeowAutoChrome.Core.Services.PluginHost;

/// <summary>
/// Responsible for executing plugin actions within the proper host context and
/// managing per-instance execution synchronization. Extracted from BrowserPluginHostCore.
/// </summary>
public sealed class PluginExecutor : IPluginExecutor
{
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
