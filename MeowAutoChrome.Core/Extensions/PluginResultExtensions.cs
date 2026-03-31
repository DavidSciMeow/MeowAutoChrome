using MeowAutoChrome.Contracts;

namespace MeowAutoChrome.Core.Extensions;

/// <summary>
/// 插件结果扩展方法的 Core 层实现（已从 Contracts 迁移）。<br/>
/// Core implementation of plugin result extension helpers migrated from Contracts.
/// </summary>
public static class PluginResultExtensions
{
    /// <summary>
    /// 返回一个封装为异步任务的成功结果，包含插件状态与可选数据。<br/>
    /// Return a successful result wrapped in a Task, including plugin state and optional data.
    /// </summary>
    /// <param name="plugin">触发扩展方法的插件实例 / plugin instance.</param>
    /// <param name="message">要包含的消息 / message to include.</param>
    /// <param name="data">可选的附加数据 / optional additional data.</param>
    public static Task<IResult> Ok(this IPlugin plugin, string message, IReadOnlyDictionary<string, string?>? data = null) => Task.FromResult(plugin.OkResult(message, data));

    /// <summary>
    /// 生成一个 IResult 的成功实例，包含插件状态与可选数据。<br/>
    /// Produce a successful IResult instance that includes plugin state and optional data.
    /// </summary>
    /// <param name="plugin">触发扩展方法的插件实例 / plugin instance.</param>
    /// <param name="message">要包含的消息 / message to include.</param>
    /// <param name="data">可选的附加数据 / optional additional data.</param>
    public static IResult OkResult(this IPlugin plugin, string message, IReadOnlyDictionary<string, string?>? data = null) => Result.Ok(MergeDefaultData(plugin, data));

    private static Dictionary<string, string?> MergeDefaultData(IPlugin plugin, IReadOnlyDictionary<string, string?>? data)
    {
        ArgumentNullException.ThrowIfNull(plugin);
        var result = new Dictionary<string, string?>
        {
            ["state"] = plugin.State.ToString(),
        };

        if (data is null) return result;
        foreach (var pair in data) result[pair.Key] = pair.Value;
        return result;
    }
}
