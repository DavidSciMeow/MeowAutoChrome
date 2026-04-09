namespace MeowAutoChrome.Contracts;

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
/// 当插件查询宿主管理的浏览器实例时返回的描述信息，保持最小以避免泄露宿主内部细节。<br/>
/// Descriptor returned to plugins when querying information about a browser instance managed by the host.
/// </summary>
public sealed record PluginBrowserInstanceInfo(
    string InstanceId,
    string? DisplayName,
    string? UserDataDirectory,
    string? OwnerId,
    bool IsCurrent
);
