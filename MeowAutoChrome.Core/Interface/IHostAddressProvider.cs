namespace MeowAutoChrome.Core.Interface;

/// <summary>
/// 提供宿主当前基础地址的抽象。<br/>
/// Abstraction that provides the host's current base address.
/// </summary>
public interface IHostAddressProvider
{
    /// <summary>
    /// 宿主对外暴露的基础地址；若暂不可用则返回 null。<br/>
    /// Base address exposed by the host, or null when not yet available.
    /// </summary>
    string? BaseAddress { get; }
}