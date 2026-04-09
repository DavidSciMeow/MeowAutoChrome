using MeowAutoChrome.Contracts;

namespace MeowAutoChrome.Core.Extensions;

/// <summary>
/// 插件结果扩展方法的 Core 层实现。当前仅负责封装附加数据，不再从插件实例读取运行状态。<br/>
/// Core implementation of plugin result extension helpers. It now only wraps additional data and no longer reads runtime state from the plugin instance.
/// </summary>
public static class PluginResultExtensions
{
    /// <summary>
    /// 返回一个封装为异步任务的成功结果，包含可选数据。<br/>
    /// Return a successful result wrapped in a Task, including optional data.
    /// </summary>
    /// <param name="plugin">触发扩展方法的插件实例 / plugin instance.</param>
    /// <param name="message">要包含的消息 / message to include.</param>
    /// <param name="data">可选的附加数据 / optional additional data.</param>
    public static Task<IResult> Ok(this IPlugin plugin, string message, IReadOnlyDictionary<string, string?>? data = null) => Task.FromResult(plugin.OkResult(message, data));

    /// <summary>
    /// 生成一个 IResult 的成功实例，包含可选数据。<br/>
    /// Produce a successful IResult instance that includes optional data.
    /// </summary>
    /// <param name="plugin">触发扩展方法的插件实例 / plugin instance.</param>
    /// <param name="message">要包含的消息 / message to include.</param>
    /// <param name="data">可选的附加数据 / optional additional data.</param>
    public static IResult OkResult(this IPlugin plugin, string message, IReadOnlyDictionary<string, string?>? data = null) => Result.Ok(MergeDefaultData(data));

    private static Dictionary<string, string?> MergeDefaultData(IReadOnlyDictionary<string, string?>? data)
    {
        var result = new Dictionary<string, string?>();

        if (data is null) return result;
        foreach (var pair in data) result[pair.Key] = pair.Value;
        return result;
    }
}
