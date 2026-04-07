using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace MeowAutoChrome.Contracts;

/// <summary>
/// 最小化的插件可见上下文，只包含只读句柄和取消令牌，插件应仅依赖此外观接口。<br/>
/// Minimal plugin-facing context containing only readonly handles and a cancellation token. Plugins should depend only on this facade.
/// </summary>
public interface IPluginContext
{
    /// <summary>
    /// 宿主提供的浏览器上下文句柄。<br/>
    /// Browser context handle provided by the host.
    /// </summary>
    IBrowserContext BrowserContext { get; }
    /// <summary>
    /// 当前活动页面（可能为 null）。<br/>
    /// The currently active page, or null if none.
    /// </summary>
    IPage? ActivePage { get; }
    /// <summary>
    /// 插件的唯一标识符。<br/>
    /// Unique identifier for the plugin.
    /// </summary>
    string PluginId { get; }
    /// <summary>
    /// 传递给插件的只读参数字典（参数名 -> 值或 null）。<br/>
    /// Read-only dictionary of arguments passed to the plugin (name -> value or null).
    /// </summary>
    IReadOnlyDictionary<string, string?> Arguments { get; }
    /// <summary>
    /// 用于取消插件操作的取消令牌。<br/>
    /// Cancellation token used to cancel plugin operations.
    /// </summary>
    CancellationToken CancellationToken { get; }

    /// <summary>
    /// 请求宿主创建新的浏览器实例或上下文，使用提供的选项进行配置。<br/>
    /// Request the host to create a new browser instance or context configured by the provided options.
    /// </summary>
    /// <param name="options">浏览器创建选项。<br/>Browser creation options.</param>
    /// <param name="cancellationToken">可选的取消令牌。<br/>Optional cancellation token.</param>
    /// <returns>返回新的实例 ID，失败时为 null。<br/>Returns the new instance id string, or null on failure.</returns>
    Task<string?> RequestNewBrowserInstanceAsync(BrowserCreationOptions options, CancellationToken cancellationToken = default);
    /// <summary>
    /// 请求宿主关于已知浏览器实例的信息。<br/>
    /// Request information about a browser instance known to the host.
    /// </summary>
    /// <param name="instanceId">要查询的实例 ID。<br/>Instance id to query.</param>
    /// <param name="cancellationToken">可选的取消令牌。<br/>Optional cancellation token.</param>
    /// <returns>若找不到或宿主不公开元数据则返回 null。<br/>Returns null if the instance is not found or host does not expose metadata.</returns>
    Task<PluginBrowserInstanceInfo?> GetBrowserInstanceInfoAsync(string instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 允许插件将日志写入宿主应用的集中日志系统。插件可以调用此方法来记录自己的运行时信息或错误。
    /// </summary>
    /// <param name="level">日志级别字符串（例如 Debug/Information/Warning/Error）。</param>
    /// <param name="message">日志消息文本。</param>
    /// <param name="category">可选日志类别；若为空，宿主将使用插件 ID 作为类别。</param>
    /// <returns>异步完成任务。</returns>
    Task WriteLogAsync(string level, string message, string? category = null);
}
