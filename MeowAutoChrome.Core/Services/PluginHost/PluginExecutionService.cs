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

    public Task<IResult> ExecuteAsync(RuntimeBrowserPluginInstance instance, IPluginContext hostContext, Func<IPlugin, Task<IResult>> execute, CancellationToken cancellationToken)
        => _executor.ExecuteAsync(instance, hostContext, execute, cancellationToken);

    public Task<IResult> ExecuteActionAsync(RuntimeBrowserPluginInstance instance, RuntimeBrowserPluginAction action, IPluginContext hostContext, CancellationToken cancellationToken)
    {
        return _executor.ExecuteAsync(instance, hostContext, async pluginInstance =>
        {
            var invocation = action.Method.Invoke(pluginInstance, PluginParameterBinder.BuildInvocationArguments(action.Method, (IPluginContext)hostContext));
            // Normalize various return shapes into IResult
            if (invocation is Task<IResult> taskResult)
                return await taskResult.ConfigureAwait(false);
            if (invocation is IResult directResult)
                return directResult;
            if (invocation is Task task)
            {
                await task.ConfigureAwait(false);
                var resultProp = task.GetType().GetProperty("Result");
                if (resultProp is not null)
                {
                    var res = resultProp.GetValue(task);
                    if (res is IResult ir) return ir;
                    return new Result(res);
                }

                // void-returning Task -> success without data
                return Result.Ok();
            }

            // synchronous returns
            if (invocation is IResult ir2) return ir2;
            if (invocation is null) return Result.Ok();
            return new Result(invocation);
        }, cancellationToken);
    }

    public Task<IResult> ExecuteControlAsync(RuntimeBrowserPluginInstance instance, string command, IPluginContext hostContext, CancellationToken cancellationToken)
    {
        return _executor.ExecuteAsync(instance, hostContext, pluginInstance => command.ToLowerInvariant() switch
        {
            "start" => pluginInstance.StartAsync(),
            "stop" => pluginInstance.StopAsync(),
            "pause" => pluginInstance.PauseAsync(),
            "resume" => pluginInstance.ResumeAsync(),
            _ => Task.FromResult<IResult>(Result.Fail($"不支持的插件控制命令：{command}"))
        }, cancellationToken);
    }
}
