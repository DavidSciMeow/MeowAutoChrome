namespace MeowAutoChrome.Contracts;

/// <summary>
/// 插件状态枚举，表示插件当前的运行状态，包括停止、运行和暂停三种状态
/// </summary>
public enum PluginState
{
    Stopped,
    Running,
    Paused
}

/// <summary>
/// 插件操作结果抽象。插件现在可以返回任意类型，宿主会将返回值规一化为 IResult。
/// </summary>
public interface IResult
{
    bool Success { get; }
    object? Data { get; }
    string? Message { get; }
}

public interface IResult<T> : IResult
{
    T? Value { get; }
}

public sealed record Result(object? Data, bool Success = true, string? Message = null) : IResult
{
    object? IResult.Data => Data;
    bool IResult.Success => Success;
    string? IResult.Message => Message;

    public static Result Ok(object? data = null) => new(data, true, null);
    public static Result Fail(string? message) => new(null, false, message);
}

public sealed record Result<T>(T? Value, bool Success = true, string? Message = null) : IResult<T>
{
    public T? Value { get; init; } = Value;
    object? IResult.Data => Value;
    bool IResult.Success => Success;
    string? IResult.Message => Message;

    public static Result<T> Ok(T? value) => new(value, true, null);
    public static Result<T> Fail(string? message) => new(default, false, message);
}

/// <summary>
/// Options used by plugins to request creation of a new browser context/instance from the host.
/// This record lives in Contracts so plugins can construct creation requests in a type-safe manner.
/// </summary>
/// <param name="OwnerId"></param>
/// <param name="UserDataDirectory"></param>
/// <param name="BrowserType"></param>
/// <param name="Headless"></param>
/// <param name="UserAgent"></param>
/// <param name="DisplayName"></param>
/// <param name="Args"></param>
public sealed record BrowserCreationOptions
(
    string? OwnerId = null,
    string? UserDataDirectory = null,
    string BrowserType = "chromium",
    bool Headless = true,
    string? UserAgent = null,
    string? DisplayName = null,
    string[]? Args = null
);

/// <summary>
/// Descriptor returned to plugins when querying information about a browser instance managed by the host.
/// Kept minimal to avoid leaking host internals.
/// </summary>
public sealed record PluginBrowserInstanceInfo(
    string InstanceId,
    string? DisplayName,
    string? UserDataDirectory,
    string? OwnerId,
    bool IsCurrent
);
