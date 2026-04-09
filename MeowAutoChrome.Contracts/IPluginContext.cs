using Microsoft.Playwright;

namespace MeowAutoChrome.Contracts;

/// <summary>
/// 插件执行期间由宿主注入的上下文。它聚合了宿主能力、当前浏览器实例以及调用参数。<br/>
/// Host-injected context available during plugin execution. It aggregates host capabilities, the current browser instance, and invocation arguments.
/// </summary>
public interface IPluginContext
{
    /// <summary>
    /// 宿主能力入口。插件通过该实例访问宿主信息、浏览器实例管理能力、日志和消息发布能力。<br/>
    /// Host capability entry point. Plugins use this instance to access host information, browser-instance management, logging, and message publishing.
    /// </summary>
    IPluginHost Host { get; }

    /// <summary>
    /// 当前与本次调用绑定的浏览器实例；当宿主当前没有选中的实例时可能为 null。<br/>
    /// Browser instance bound to the current invocation. May be null when the host has no selected instance.
    /// </summary>
    IPluginBrowserInstance? CurrentBrowserInstance { get; }

    /// <summary>
    /// 当前插件拥有的浏览器实例只读列表快照。宿主会在创建、关闭或切换实例时更新该列表。<br/>
    /// Read-only snapshot of browser instances owned by the current plugin. The host updates this list when instances are created, closed, or switched.
    /// </summary>
    IReadOnlyList<IPluginBrowserInstance> Instances { get; }

    /// <summary>
    /// 当前浏览器实例的 Playwright BrowserContext；当没有绑定实例时为 null。<br/>
    /// Playwright BrowserContext of the current browser instance, or null when no instance is bound.
    /// </summary>
    IBrowserContext? BrowserContext { get; }

    /// <summary>
    /// 当前浏览器实例对应的 Playwright Browser；当浏览器上下文不可用时为 null。<br/>
    /// Playwright Browser associated with the current browser instance, or null when the browser context is unavailable.
    /// </summary>
    IBrowser? Browser { get; }

    /// <summary>
    /// 当前浏览器实例的活动页面；当没有活动页面时为 null。<br/>
    /// Active page of the current browser instance, or null when no page is active.
    /// </summary>
    IPage? ActivePage { get; }

    /// <summary>
    /// 当前绑定到插件上下文的浏览器实例 ID；当没有绑定实例时为空字符串。<br/>
    /// Browser instance id currently bound to this plugin context, or an empty string when no instance is bound.
    /// </summary>
    string BrowserInstanceId { get; }

    /// <summary>
    /// 传递给插件的只读参数字典（参数名 -> 值或 null）。<br/>
    /// Read-only dictionary of arguments passed to the plugin (parameter name -> value or null).
    /// </summary>
    IReadOnlyDictionary<string, string?> Arguments { get; }

    /// <summary>
    /// 用于取消插件操作的取消令牌。该值等同于 Host.CancellationToken，保留为便捷访问。<br/>
    /// Cancellation token used to cancel plugin operations. This mirrors Host.CancellationToken for convenience.
    /// </summary>
    CancellationToken CancellationToken { get; }
}
