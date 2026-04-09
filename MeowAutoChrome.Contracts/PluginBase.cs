using Microsoft.Playwright;

namespace MeowAutoChrome.Contracts;

/// <summary>
/// 面向插件作者的便捷抽象基类。它在保留 IPlugin 契约的同时，提供对宿主上下文、运行状态、默认实例、实例列表、浏览器实例管理和消息发布的常用访问器。<br/>
/// Convenience abstract base class for plugin authors. It preserves the IPlugin contract while providing common accessors for host context, runtime state, default instance, instance list, browser-instance management, and message publishing.
/// </summary>
public abstract class PluginBase : IPlugin
{
    /// <summary>
    /// 宿主注入的插件上下文。宿主会在生命周期方法和动作调用期间设置该属性。<br/>
    /// Host-injected plugin context. The host sets this property during lifecycle and action invocations.
    /// </summary>
    public IPluginContext? HostContext { get; set; }

    /// <summary>
    /// 是否支持暂停。默认不支持。<br/>
    /// Whether pause is supported. Defaults to false.
    /// </summary>
    public virtual bool SupportsPause => false;

    /// <summary>
    /// 当前宿主上下文；若当前不在宿主调用范围内则抛出异常。<br/>
    /// Current host context; throws when accessed outside a host invocation.
    /// </summary>
    protected IPluginContext Context => HostContext ?? throw new InvalidOperationException("No host context available.");

    /// <summary>
    /// 当前宿主 facade；若当前不在宿主调用范围内则抛出异常。<br/>
    /// Current host facade; throws when accessed outside a host invocation.
    /// </summary>
    protected IPluginHost Host => Context.Host;

    /// <summary>
    /// 当前插件运行状态，只读，由宿主管理。<br/>
    /// Current plugin runtime state, read-only and managed by the host.
    /// </summary>
    protected PluginState State => Host.State;

    /// <summary>
    /// 当前插件默认可用的浏览器实例。优先返回当前绑定且属于本插件的实例，否则返回插件实例列表中的第一个。<br/>
    /// Default browser instance for the current plugin. Prefers the currently bound instance when it belongs to this plugin; otherwise returns the first instance in the plugin-owned list.
    /// </summary>
    protected IPluginBrowserInstance? Instance
    {
        get
        {
            var current = CurrentBrowserInstance;
            if (current is not null && string.Equals(current.OwnerId, Host.PluginId, StringComparison.OrdinalIgnoreCase)) return current;
            return Instances.Count > 0 ? Instances[0] : null;
        }
    }

    /// <summary>
    /// 当前插件拥有的浏览器实例只读列表。宿主会在创建、关闭或切换实例时自动刷新。<br/>
    /// Read-only list of browser instances owned by the current plugin. The host refreshes it automatically when instances are created, closed, or switched.
    /// </summary>
    protected IReadOnlyList<IPluginBrowserInstance> Instances => Context.Instances;

    /// <summary>
    /// 当前调用的取消令牌。<br/>
    /// Cancellation token for the current invocation.
    /// </summary>
    protected CancellationToken CancellationToken => Context.CancellationToken;

    /// <summary>
    /// 当前绑定的浏览器实例。<br/>
    /// Currently bound browser instance.
    /// </summary>
    protected IPluginBrowserInstance? CurrentBrowserInstance => Context.CurrentBrowserInstance;

    /// <summary>
    /// 当前绑定实例的 BrowserContext。<br/>
    /// BrowserContext of the currently bound instance.
    /// </summary>
    protected IBrowserContext? BrowserContext => Context.BrowserContext;

    /// <summary>
    /// 当前绑定实例的 Browser。<br/>
    /// Browser of the currently bound instance.
    /// </summary>
    protected IBrowser? Browser => Context.Browser;

    /// <summary>
    /// 当前活动页面。<br/>
    /// Current active page.
    /// </summary>
    protected IPage? ActivePage => Context.ActivePage;

    /// <summary>
    /// 当前浏览器实例 Id。<br/>
    /// Current browser instance id.
    /// </summary>
    protected string BrowserInstanceId => Context.BrowserInstanceId;

    /// <summary>
    /// 本次调用参数。<br/>
    /// Invocation arguments.
    /// </summary>
    protected IReadOnlyDictionary<string, string?> Arguments => Context.Arguments;

    /// <summary>
    /// 发布插件运行过程中的消息或结构化数据。默认只写入消息记录；当 toastRequested 为 true 时，宿主应额外推送一条 toast。<br/>
    /// Publish an in-progress plugin message or structured data payload. By default it only updates the message feed; when toastRequested is true, the host should also push a toast.
    /// </summary>
    protected Task MessageAsync(string? message, IReadOnlyDictionary<string, string?>? data = null, bool toastRequested = false)
        => Host.PublishUpdateAsync(message, data, toastRequested);

    /// <summary>
    /// 发布一条应额外显示 toast 的消息。<br/>
    /// Publish a message that should additionally be displayed as a toast.
    /// </summary>
    protected Task ToastAsync(string message, IReadOnlyDictionary<string, string?>? data = null)
        => Host.ToastAsync(message, data);

    /// <summary>
    /// 将日志写入宿主日志系统。<br/>
    /// Write a log entry into the host log system.
    /// </summary>
    protected Task WriteLogAsync(string level, string message, string? category = null)
        => Host.WriteLogAsync(level, message, category);

    /// <summary>
    /// 创建浏览器实例并返回实例句柄。<br/>
    /// Create a browser instance and return its handle.
    /// </summary>
    protected Task<IPluginBrowserInstance?> CreateBrowserInstanceAsync(BrowserCreationOptions options, CancellationToken cancellationToken = default)
        => Host.CreateBrowserInstanceAsync(options, cancellationToken == default ? CancellationToken : cancellationToken);

    /// <summary>
    /// 关闭指定浏览器实例。<br/>
    /// Close the specified browser instance.
    /// </summary>
    protected Task<bool> CloseBrowserInstanceAsync(string instanceId, CancellationToken cancellationToken = default)
        => Host.CloseBrowserInstanceAsync(instanceId, cancellationToken == default ? CancellationToken : cancellationToken);

    /// <summary>
    /// 将指定浏览器实例切换为当前实例。<br/>
    /// Switch the specified browser instance to become the current instance.
    /// </summary>
    protected Task<bool> SelectBrowserInstanceAsync(string instanceId, CancellationToken cancellationToken = default)
        => Host.SelectBrowserInstanceAsync(instanceId, cancellationToken == default ? CancellationToken : cancellationToken);

    /// <summary>
    /// 启动插件并执行初始化逻辑。<br/>
    /// Start the plugin and perform initialization logic.
    /// </summary>
    public abstract Task<IResult> StartAsync();

    /// <summary>
    /// 停止插件并释放资源。<br/>
    /// Stop the plugin and release resources.
    /// </summary>
    public abstract Task<IResult> StopAsync();

    /// <summary>
    /// 暂停插件运行。<br/>
    /// Pause plugin execution.
    /// </summary>
    public abstract Task<IResult> PauseAsync();

    /// <summary>
    /// 恢复插件运行。<br/>
    /// Resume plugin execution.
    /// </summary>
    public abstract Task<IResult> ResumeAsync();
}