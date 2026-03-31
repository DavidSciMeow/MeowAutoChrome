using MeowAutoChrome.Core.Models;
using MeowAutoChrome.Contracts;
using MeowAutoChrome.Core.Services.PluginDiscovery;

namespace MeowAutoChrome.Core.Services.PluginHost;

/// <summary>
/// 插件执行服务：封装对插件执行器的调用，提供执行动作与控制命令的便捷方法。<br/>
/// Plugin execution service that wraps the plugin executor and provides helpers to execute actions and control commands.
/// </summary>
public sealed class PluginExecutionService
{
    private readonly IPluginExecutor _executor;

    /// <summary>
    /// 构造函数：注入插件执行器依赖。<br/>
    /// Constructor: injects the plugin executor dependency.
    /// </summary>
    /// <param name="executor">插件执行器 / plugin executor.</param>
    public PluginExecutionService(IPluginExecutor executor)
    {
        _executor = executor;
    }

    /// <summary>
    /// 将执行请求委托给内部的 <see cref="IPluginExecutor"/>。<br/>
    /// Delegate execution to the internal <see cref="IPluginExecutor"/>.
    /// </summary>
    /// <param name="instance">目标插件运行时实例。<br/>Target runtime plugin instance.</param>
    /// <param name="hostContext">宿主提供的插件上下文。<br/>Host-provided plugin context.</param>
    /// <param name="execute">在插件实例上执行动作的委托。<br/>Delegate that executes an action on the plugin instance.</param>
    /// <param name="cancellationToken">取消令牌，用于中止执行。<br/>Cancellation token to abort execution.</param>
    /// <returns>异步操作，返回规范化的执行结果。<br/>Asynchronous operation returning the normalized execution result.</returns>
    public Task<IResult> ExecuteAsync(RuntimeBrowserPluginInstance instance, IPluginContext hostContext, Func<IPlugin, Task<IResult>> execute, CancellationToken cancellationToken)
        => _executor.ExecuteAsync(instance, hostContext, execute, cancellationToken);

    /// <summary>
    /// 执行指定动作并标准化返回为 `IResult`。<br/>
    /// Execute the specified action and normalize the return to `IResult`.
    /// </summary>
    /// <param name="instance">目标插件运行时实例。<br/>Target runtime plugin instance.</param>
    /// <param name="action">要执行的动作元数据。<br/>Action metadata to execute.</param>
    /// <param name="hostContext">宿主提供的插件上下文。<br/>Host-provided plugin context.</param>
    /// <param name="cancellationToken">取消令牌，用于在外部取消执行。<br/>Cancellation token for external cancellation.</param>
    /// <returns>异步操作，返回规范化的执行结果。<br/>Asynchronous operation returning the normalized execution result.</returns>
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

    /// <summary>
    /// 执行插件的控制命令（start/stop/pause/resume）。<br/>
    /// Execute a control command for the plugin (start/stop/pause/resume).
    /// </summary>
    /// <param name="instance">目标插件运行时实例。<br/>Target runtime plugin instance.</param>
    /// <param name="command">控制命令字符串（例如 start/stop/pause/resume）。<br/>Control command string (e.g., start/stop/pause/resume).</param>
    /// <param name="hostContext">宿主提供的插件上下文。<br/>Host-provided plugin context.</param>
    /// <param name="cancellationToken">取消令牌，用于中止执行。<br/>Cancellation token to abort execution.</param>
    /// <returns>异步操作，返回规范化的执行结果。<br/>Asynchronous operation returning the normalized execution result.</returns>
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
