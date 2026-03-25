using MeowAutoChrome.Contracts.Interface;
using MeowAutoChrome.Contracts.Abstractions;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MeowAutoChrome.Contracts.BrowserPlugin;

/// <summary>
/// 插件结果扩展方法，提供便捷的方式创建操作结果对象，并自动合并默认数据（如插件状态）到结果数据中
/// </summary>
public static class PluginResultExtensions
{
    /// <summary>
    /// 插件操作成功的结果，包含消息和数据，数据会自动合并插件当前状态等默认信息，方便插件在执行操作后返回结果给宿主或前端展示
    /// </summary>
    /// <param name="plugin">插件</param>
    /// <param name="message">消息</param>
    /// <param name="data">数据</param>
    /// <returns></returns>
    public static Task<PluginActionResult> Ok(this IPlugin plugin, string message, IReadOnlyDictionary<string, string?>? data = null) => Task.FromResult(plugin.OkResult(message, data));
    /// <summary>
    /// 插件操作成功的结果，包含消息和数据，数据会自动合并插件当前状态等默认信息，方便插件在执行操作后返回结果给宿主或前端展示
    /// </summary>
    /// <param name="plugin">插件</param>
    /// <param name="message">消息</param>
    /// <param name="data">数据</param>
    /// <returns></returns>
    public static PluginActionResult OkResult(this IPlugin plugin, string message, IReadOnlyDictionary<string, string?>? data = null) => new(message, MergeDefaultData(plugin, data));
    /// <summary>
    /// 合成插件的结果数据，将插件当前状态等默认信息与传入的数据合并，确保结果数据包含必要的上下文信息，方便宿主或前端根据这些信息进行处理和展示
    /// </summary>
    /// <param name="plugin">插件</param>
    /// <param name="data">数据</param>
    /// <returns></returns>
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
