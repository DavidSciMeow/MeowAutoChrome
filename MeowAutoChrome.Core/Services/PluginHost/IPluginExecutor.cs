using MeowAutoChrome.Core.Models;
using MeowAutoChrome.Contracts;

namespace MeowAutoChrome.Core.Services.PluginHost;

/// <summary>
/// 插件执行器接口，负责在给定运行时实例与宿主上下文中执行插件调用。<br/>
/// Interface responsible for executing plugin calls within a given runtime instance and host context.
/// </summary>
public interface IPluginExecutor
{
    /// <summary>
    /// 在指定实例与宿主上下文中执行传入的插件回调并返回结果。<br/>
    /// Execute the provided plugin callback within the specified instance and host context, returning the result.
    /// </summary>
    /// <param name="instance">运行时插件实例。<br/>Runtime plugin instance.</param>
    /// <param name="hostContext">注入的宿主上下文。<br/>Injected host context.</param>
    /// <param name="execute">实际针对插件的执行回调。<br/>Callback that performs the plugin execution.</param>
    /// <param name="cancellationToken">取消令牌。<br/>Cancellation token.</param>
    Task<IResult> ExecuteAsync(RuntimeBrowserPluginInstance instance, IPluginContext hostContext, Func<IPlugin, Task<IResult>> execute, CancellationToken cancellationToken);

}
