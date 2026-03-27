using System;
using System.Threading;
using System.Threading.Tasks;
using MeowAutoChrome.Core.Models;
using MeowAutoChrome.Contracts;
using MeowAutoChrome.Core.Services.PluginDiscovery;
using System.Reflection;

namespace MeowAutoChrome.Core.Services.PluginHost;

public sealed class PluginExecutionService
{
    private readonly IPluginExecutor _executor;

    public PluginExecutionService(IPluginExecutor executor)
    {
        _executor = executor;
    }

    public Task<PAResult> ExecuteAsync(RuntimeBrowserPluginInstance instance, IPluginContext hostContext, Func<IPlugin, Task<PAResult>> execute, CancellationToken cancellationToken)
        => _executor.ExecuteAsync(instance, hostContext, execute, cancellationToken);

    public Task<PAResult> ExecuteActionAsync(RuntimeBrowserPluginInstance instance, RuntimeBrowserPluginAction action, IPluginContext hostContext, CancellationToken cancellationToken)
    {
        return _executor.ExecuteAsync(instance, hostContext, async pluginInstance =>
        {
            var invocation = action.Method.Invoke(pluginInstance, PluginParameterBinder.BuildInvocationArguments(action.Method, (IPluginContext)hostContext));
            if (invocation is Task<PAResult> task)
                return await task.ConfigureAwait(false);
            if (invocation is Task genericTask)
            {
                await genericTask.ConfigureAwait(false);
                var resultProp = genericTask.GetType().GetProperty("Result");
                if (resultProp is not null)
                {
                    var res = resultProp.GetValue(genericTask);
                    if (res is PAResult par)
                        return par;
                }
            }
            throw new InvalidOperationException($"插件动作返回类型无效：{action.Method.DeclaringType?.FullName}.{action.Method.Name}");
        }, cancellationToken);
    }

    public Task<PAResult> ExecuteControlAsync(RuntimeBrowserPluginInstance instance, string command, IPluginContext hostContext, CancellationToken cancellationToken)
    {
        return _executor.ExecuteAsync(instance, hostContext, pluginInstance => command.ToLowerInvariant() switch
        {
            "start" => pluginInstance.StartAsync(),
            "stop" => pluginInstance.StopAsync(),
            "pause" => pluginInstance.PauseAsync(),
            "resume" => pluginInstance.ResumeAsync(),
            _ => Task.FromResult(new PAResult($"不支持的插件控制命令：{command}", null))
        }, cancellationToken);
    }
}
