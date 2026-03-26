using System;
using System.Threading;
using System.Threading.Tasks;
using MeowAutoChrome.Contracts.Abstractions;
using MeowAutoChrome.Core.Models;
using MeowAutoChrome.Contracts;
using MeowAutoChrome.Contracts.BrowserPlugin;

namespace MeowAutoChrome.Core.Services.PluginHost;

public interface IPluginExecutor
{
    Task<PAResult> ExecuteAsync(RuntimeBrowserPluginInstance instance, IHostContext hostContext, Func<IPlugin, Task<PAResult>> execute, CancellationToken cancellationToken);

}
