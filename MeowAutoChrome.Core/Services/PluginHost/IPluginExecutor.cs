using System;
using System.Threading;
using System.Threading.Tasks;
using MeowAutoChrome.Contracts.Abstractions;
using MeowAutoChrome.Core.Models;
using MeowAutoChrome.Contracts.Interface;
using MeowAutoChrome.Contracts.BrowserPlugin;

namespace MeowAutoChrome.Core.Services.PluginHost;

public interface IPluginExecutor
{
    Task<PluginActionResult> ExecuteAsync(RuntimeBrowserPluginInstance instance, IHostContext hostContext, Func<IPlugin, Task<PluginActionResult>> execute, CancellationToken cancellationToken);
}
