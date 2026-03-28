using MeowAutoChrome.Core.Models;
using MeowAutoChrome.Contracts;

namespace MeowAutoChrome.Core.Services.PluginHost;

public interface IPluginExecutor
{
    Task<IResult> ExecuteAsync(RuntimeBrowserPluginInstance instance, IPluginContext hostContext, Func<IPlugin, Task<IResult>> execute, CancellationToken cancellationToken);

}
