using System;
using System.Threading;
using System.Threading.Tasks;
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
    public async Task<PAResult> ExecuteAsync(RuntimeBrowserPluginInstance instance, IPluginContext hostContext, Func<MeowAutoChrome.Contracts.IPlugin, Task<PAResult>> execute, CancellationToken cancellationToken)
    {
        await instance.ExecutionLock.WaitAsync(cancellationToken);

        try
        {
            // Assign to plugin HostContext (Contracts facade) for backward compatibility.
            // In future revisions we'll provide a Core-hosted ICorePluginContext adapter instead.
            instance.Instance.HostContext = hostContext;

            try
            {
                return await execute((MeowAutoChrome.Contracts.IPlugin)instance.Instance);
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
