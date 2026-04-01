using MeowAutoChrome.Core.Services;
using Microsoft.AspNetCore.SignalR;
using MeowAutoChrome.WebAPI.Hubs;
using MeowAutoChrome.Contracts.SignalR;
using System.Threading;
using System.Threading.Tasks;

namespace MeowAutoChrome.WebAPI.Services;

/// <summary>
/// 基于 SignalR 的推流帧发送器。<br/>
/// SignalR-based screencast frame sink.
/// </summary>
public sealed class SignalRScreencastFrameSink : IScreencastFrameSink
{
    private readonly IHubContext<BrowserHub, IBrowserClient> _hub;

    /// <summary>
    /// 初始化帧发送器。<br/>
    /// Initialize the frame sink.
    /// </summary>
    /// <param name="hub">带客户端契约的 Hub 上下文。<br/>Hub context with client contract.</param>
    public SignalRScreencastFrameSink(IHubContext<BrowserHub, IBrowserClient> hub) => _hub = hub;

    /// <summary>
    /// 向所有连接客户端发送一帧推流数据。<br/>
    /// Send one screencast frame to all connected clients.
    /// </summary>
    public Task SendFrameAsync(string data, int? width, int? height, CancellationToken cancellationToken = default)
        => _hub.Clients.All.ReceiveFrame(data, width, height);

    /// <summary>
    /// 通知所有客户端当前未启用实时画面。<br/>
    /// Notify all clients that screencast is currently disabled.
    /// </summary>
    public Task NotifyScreencastDisabledAsync(CancellationToken cancellationToken = default)
        => _hub.Clients.All.ScreencastDisabled();
}
