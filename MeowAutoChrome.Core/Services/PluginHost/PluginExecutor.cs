using System;
using System.Threading;
using System.Threading.Tasks;
using MeowAutoChrome.Contracts.Abstractions;
using MeowAutoChrome.Core.Models;
using MeowAutoChrome.Contracts.Interface;
using MeowAutoChrome.Contracts.BrowserPlugin;

namespace MeowAutoChrome.Core.Services.PluginHost;

/// <summary>
/// Responsible for executing plugin actions within the proper host context and
/// managing per-instance execution synchronization. Extracted from BrowserPluginHostCore.
/// </summary>
public sealed class PluginExecutor : IPluginExecutor
{
    public async Task<PluginActionResult> ExecuteAsync(RuntimeBrowserPluginInstance instance, IHostContext hostContext, Func<IPlugin, Task<PluginActionResult>> execute, CancellationToken cancellationToken)
    {
        await instance.ExecutionLock.WaitAsync(cancellationToken);

        try
        {
            instance.Instance.HostContext = hostContext;

            try
            {
                return await execute(instance.Instance);
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
