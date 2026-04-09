using MeowAutoChrome.Core.Interface;

namespace MeowAutoChrome.WebAPI.Services;

/// <summary>
/// 保存 WebAPI 宿主当前基础地址的可变实现。<br/>
/// Mutable implementation that stores the WebAPI host's current base address.
/// </summary>
public sealed class HostAddressProvider : IHostAddressProvider
{
    /// <summary>
    /// 宿主当前基础地址；在应用完成监听前可能为 null。<br/>
    /// Current host base address; may be null before the application starts listening.
    /// </summary>
    public string? BaseAddress { get; set; }
}