using System;
using System.Threading;
using System.Threading.Tasks;
using MeowAutoChrome.Core.Models;
using MeowAutoChrome.Contracts;

namespace MeowAutoChrome.Core.Services.PluginHost;

public interface IPluginExecutor
{
    Task<PAResult> ExecuteAsync(RuntimeBrowserPluginInstance instance, IPluginContext hostContext, Func<IPlugin, Task<PAResult>> execute, CancellationToken cancellationToken);

}
