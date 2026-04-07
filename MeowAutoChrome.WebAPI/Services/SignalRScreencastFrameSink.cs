using MeowAutoChrome.Core.Services;
using Microsoft.AspNetCore.SignalR;
using MeowAutoChrome.WebAPI.Hubs;
using MeowAutoChrome.Contracts.SignalR;

namespace MeowAutoChrome.WebAPI.Services;

/// <summary>
/// 基于 SignalR 的推流帧发送器。<br/>
/// SignalR-based screencast frame sink.
/// </summary>
/// <remarks>
/// 初始化帧发送器。<br/>
/// Initialize the frame sink.
/// </remarks>
/// <param name="hub">带客户端契约的 Hub 上下文。<br/>Hub context with client contract.</param>
public sealed class SignalRScreencastFrameSink(IHubContext<BrowserHub, IBrowserClient> hub) : IScreencastFrameSink
{

    /// <summary>
    /// 将推流帧发送给所有连接的客户端。<br/>
    /// Send screencast frames to all connected clients.
    /// </summary>
    /// <param name="data">帧数据。<br/>Frame data.</param>
    /// <param name="width">帧宽度。<br/>Frame width.</param>
    /// <param name="height">帧高度。<br/>Frame height.</param>
    /// <param name="cancellationToken">取消操作的令牌。<br/>Cancellation token to cancel the operation.</param>
    /// <returns>表示异步操作的任务。<br/>A task representing the asynchronous operation.</returns>
    public Task SendFrameAsync(string data, int? width, int? height, CancellationToken cancellationToken = default) => hub.Clients.All.ReceiveFrame(data, width, height);

    /// <summary>
    /// 通知所有连接客户端推流已停止。<br/>
    /// Notify all connected clients that screencast has been disabled.
    /// </summary>
    /// <param name="cancellationToken">取消操作的令牌。<br/>Cancellation token to cancel the operation.</param>
    /// <returns>表示异步操作的任务。<br/>A task representing the asynchronous operation.</returns>
    public Task NotifyScreencastDisabledAsync(CancellationToken cancellationToken = default) => hub.Clients.All.ScreencastDisabled();
}
