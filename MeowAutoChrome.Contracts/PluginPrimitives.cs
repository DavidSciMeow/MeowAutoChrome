namespace MeowAutoChrome.Contracts;

using Microsoft.Playwright;

/// <summary>
/// 插件状态枚举，表示插件当前的运行状态，包括停止、运行和暂停三种状态。<br/>
/// Plugin state enum indicating the current running state of a plugin: Stopped, Running, or Paused.
/// </summary>
public enum PluginState
{
    /// <summary>
    /// 插件已停止。<br/>
    /// The plugin is stopped.
    /// </summary>
    Stopped,

    /// <summary>
    /// 插件正在运行。<br/>
    /// The plugin is running.
    /// </summary>
    Running,

    /// <summary>
    /// 插件已暂停。<br/>
    /// The plugin is paused.
    /// </summary>
    Paused
}

/// <summary>
/// 插件操作结果的抽象表示，宿主会将插件返回值规范化为此接口。<br/>
/// Abstract representation of a plugin operation result; the host normalizes plugin return values to this interface.
/// </summary>
public interface IResult
{
    /// <summary>
    /// 表示操作是否成功。<br/>
    /// Indicates whether the operation was successful.
    /// </summary>
    bool Success { get; }
    /// <summary>
    /// 额外返回的数据（可为空）。<br/>
    /// Additional data returned by the operation (may be null).
    /// </summary>
    object? Data { get; }
    /// <summary>
    /// 可选的消息或错误说明。<br/>
    /// Optional message or error description.
    /// </summary>
    string? Message { get; }
}

/// <summary>
/// 泛型操作结果接口：在 `IResult` 基础上提供类型化的 `Value` 属性。<br/>
/// Generic operation result interface that extends `IResult` with a typed `Value` property.
/// </summary>
public interface IResult<T> : IResult
{
    /// <summary>
    /// 泛型值，表示操作返回的具体类型值。<br/>
    /// Generic value representing the typed result of the operation.
    /// </summary>
    T? Value { get; }
}

/// <summary>
/// 非泛型的操作结果实现。<br/>
/// Non-generic implementation of an operation result.
/// </summary>
public sealed record Result(object? Data, bool Success = true, string? Message = null) : IResult
{
    object? IResult.Data => Data;
    bool IResult.Success => Success;
    string? IResult.Message => Message;

    /// <summary>
    /// 创建一个成功的 `Result` 操作。<br/>
    /// Create a successful `Result` operation.
    /// </summary>
    /// <param name="data">可选返回数据 / optional data.</param>
    /// <returns>表示成功的 `Result` 对象 / A `Result` indicating success.</returns>
    public static Result Ok(object? data = null) => new(data, true, null);

    /// <summary>
    /// 创建一个失败的 `Result` 操作。<br/>
    /// Create a failed `Result` operation.
    /// </summary>
    /// <param name="message">错误消息 / error message.</param>
    /// <returns>表示失败的 `Result` 对象 / A `Result` indicating failure.</returns>
    public static Result Fail(string? message) => new(null, false, message);
}

/// <summary>
/// 泛型的操作结果实现，包含类型化的 `Value`。<br/>
/// Generic implementation of an operation result that carries a typed `Value`.
/// </summary>
public sealed record Result<T>(T? Value, bool Success = true, string? Message = null) : IResult<T>
{
    /// <summary>
    /// 泛型结果中封装的具体值（用于序列化与访问）。<br/>
    /// The concrete value carried by the generic result (used for serialization and access).
    /// </summary>
    public T? Value { get; init; } = Value;
    object? IResult.Data => Value;
    bool IResult.Success => Success;
    string? IResult.Message => Message;

    /// <summary>
    /// 创建一个成功的 `Result{T}`。<br/>
    /// Create a successful `Result{T}`.
    /// </summary>
    /// <param name="value">要封装的值 / value to wrap.</param>
    /// <returns>表示成功的泛型结果 / A successful generic result.</returns>
    public static Result<T> Ok(T? value) => new(value, true, null);

    /// <summary>
    /// 创建一个失败的 `Result{T}`。<br/>
    /// Create a failed `Result{T}`.
    /// </summary>
    /// <param name="message">错误消息 / error message.</param>
    /// <returns>表示失败的泛型结果 / A failed generic result.</returns>
    public static Result<T> Fail(string? message) => new(default, false, message);
}

/// <summary>
/// 插件向宿主请求创建浏览器实例/上下文时使用的选项。<br/>
/// Options used by plugins to request the creation of a new browser context/instance from the host.
/// </summary>
/// <param name="OwnerId">可选的所有者 ID，用于标识请求者。<br/>Optional owner id to identify the requester.</param>
/// <param name="UserDataDirectory">可选的用户数据目录路径。<br/>Optional user data directory path.</param>
/// <param name="BrowserType">浏览器类型（例如 "chromium"）。<br/>Browser type, e.g. "chromium".</param>
/// <param name="Headless">是否以无头模式启动。<br/>Whether to start in headless mode.</param>
/// <param name="UserAgent">可选的用户代理字符串。<br/>Optional user agent string.</param>
/// <param name="DisplayName">可选的显示名称。<br/>Optional display name.</param>
/// <param name="Args">传递给浏览器的命令行参数数组。<br/>Array of command line arguments passed to the browser.</param>
/// <param name="RequestedInstanceId">可选的目标实例 ID；提供时宿主会严格按该值创建，若已存在则失败。<br/>Optional requested instance id; when provided, the host uses this exact value and fails if it is already in use.</param>
public sealed record BrowserCreationOptions
(
    string? OwnerId = null,
    string? UserDataDirectory = null,
    string BrowserType = "chromium",
    bool Headless = true,
    string? UserAgent = null,
    string? DisplayName = null,
    string[]? Args = null,
    string? RequestedInstanceId = null
);

/// <summary>
/// 插件可见的宿主入口。它暴露宿主元数据、浏览器实例管理能力以及日志/消息发布能力。<br/>
/// Host entry point visible to plugins. It exposes host metadata, browser-instance management capabilities, and logging/message publishing.
/// </summary>
public interface IPluginHost
{
    /// <summary>
    /// 当前插件的唯一标识符。<br/>
    /// Unique identifier of the current plugin.
    /// </summary>
    string PluginId { get; }

    /// <summary>
    /// 当前调用目标标识，例如生命周期命令或动作 Id。<br/>
    /// Identifier of the current invocation target, such as a lifecycle command or action id.
    /// </summary>
    string TargetId { get; }

    /// <summary>
    /// 当前插件的运行状态。该值由宿主维护，插件只读不可写。<br/>
    /// Current runtime state of the plugin. The host owns this value and plugins can only read it.
    /// </summary>
    PluginState State { get; }

    /// <summary>
    /// 宿主当前对外暴露的基础地址；当宿主未提供时可能为 null。<br/>
    /// Host base address currently exposed externally; may be null when the host does not provide one.
    /// </summary>
    string? BaseAddress { get; }

    /// <summary>
    /// 当前插件执行对应的取消令牌。<br/>
    /// Cancellation token associated with the current plugin execution.
    /// </summary>
    CancellationToken CancellationToken { get; }

    /// <summary>
    /// 返回当前插件拥有的全部浏览器实例。<br/>
    /// Return all browser instances owned by the current plugin.
    /// </summary>
    Task<IReadOnlyList<IPluginBrowserInstance>> GetOwnedBrowserInstancesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据实例 Id 返回当前插件可访问的浏览器实例。<br/>
    /// Return a browser instance accessible to the current plugin by instance id.
    /// </summary>
    Task<IPluginBrowserInstance?> GetBrowserInstanceAsync(string instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 请求宿主创建新的浏览器实例，并返回创建后的实例句柄。<br/>
    /// Request the host to create a new browser instance and return the resulting instance handle.
    /// </summary>
    Task<IPluginBrowserInstance?> CreateBrowserInstanceAsync(BrowserCreationOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// 请求宿主关闭指定浏览器实例。<br/>
    /// Request the host to close the specified browser instance.
    /// </summary>
    Task<bool> CloseBrowserInstanceAsync(string instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 请求宿主将指定浏览器实例切换为当前实例。<br/>
    /// Request the host to switch the specified browser instance to become the current instance.
    /// </summary>
    Task<bool> SelectBrowserInstanceAsync(string instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 允许插件将日志写入宿主应用的集中日志系统。<br/>
    /// Allow the plugin to write logs into the host application's centralized log system.
    /// </summary>
    Task WriteLogAsync(string level, string message, string? category = null);

    /// <summary>
    /// 向宿主推送插件运行过程中的消息或结构化数据更新。默认仅写入消息记录；当 toastRequested 为 true 时，宿主应额外显示 toast。<br/>
    /// Publish an in-progress plugin message or structured data update to the host. By default it only updates the message feed; when toastRequested is true, the host should additionally display a toast.
    /// </summary>
    Task PublishUpdateAsync(string? message, IReadOnlyDictionary<string, string?>? data = null, bool toastRequested = false);

    /// <summary>
    /// 向宿主推送一条应额外显示 toast 的消息。<br/>
    /// Publish a message that should additionally be displayed as a toast by the host.
    /// </summary>
    Task ToastAsync(string message, IReadOnlyDictionary<string, string?>? data = null);
}

/// <summary>
/// 插件可见的浏览器页面句柄。<br/>
/// Plugin-visible browser page handle.
/// </summary>
public interface IPluginBrowserPage
{
    /// <summary>
    /// 页面对应的宿主内部页签 Id。<br/>
    /// Host-internal tab identifier associated with the page.
    /// </summary>
    string PageId { get; }

    /// <summary>
    /// Playwright 页面对象。<br/>
    /// Playwright page object.
    /// </summary>
    IPage Page { get; }

    /// <summary>
    /// 当前页面是否为该实例的选中页面。<br/>
    /// Whether this page is currently selected for the instance.
    /// </summary>
    bool IsSelected { get; }
}

/// <summary>
/// 插件可见的浏览器实例句柄。<br/>
/// Plugin-visible browser instance handle.
/// </summary>
public interface IPluginBrowserInstance
{
    /// <summary>
    /// 浏览器实例 Id。<br/>
    /// Browser instance id.
    /// </summary>
    string InstanceId { get; }

    /// <summary>
    /// 浏览器实例显示名称。<br/>
    /// Browser instance display name.
    /// </summary>
    string? DisplayName { get; }

    /// <summary>
    /// 浏览器实例使用的用户数据目录。<br/>
    /// User-data directory used by the browser instance.
    /// </summary>
    string? UserDataDirectory { get; }

    /// <summary>
    /// 浏览器实例的拥有者标识。<br/>
    /// Owner identifier of the browser instance.
    /// </summary>
    string? OwnerId { get; }

    /// <summary>
    /// 当前实例是否为宿主选中的实例。<br/>
    /// Whether the instance is currently selected by the host.
    /// </summary>
    bool IsCurrent { get; }

    /// <summary>
    /// Playwright Browser 对象；在持久化上下文未公开 Browser 时可能为 null。<br/>
    /// Playwright Browser object; may be null when the persistent context does not expose a Browser instance.
    /// </summary>
    IBrowser? Browser { get; }

    /// <summary>
    /// Playwright BrowserContext 对象。<br/>
    /// Playwright BrowserContext object.
    /// </summary>
    IBrowserContext? BrowserContext { get; }

    /// <summary>
    /// 当前活动页面。<br/>
    /// Current active page.
    /// </summary>
    IPage? ActivePage { get; }

    /// <summary>
    /// 当前选中的页面 Id。<br/>
    /// Currently selected page id.
    /// </summary>
    string? SelectedPageId { get; }

    /// <summary>
    /// 当前实例下的页面列表。<br/>
    /// Pages currently available under this instance.
    /// </summary>
    IReadOnlyList<IPluginBrowserPage> Pages { get; }
}
