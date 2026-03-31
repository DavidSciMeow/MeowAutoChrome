using MeowAutoChrome.Core.Services;
using Microsoft.AspNetCore.SignalR;
using MeowAutoChrome.Web.Hubs;
using MeowAutoChrome.Contracts.SignalR;

namespace MeowAutoChrome.Web.Services;

/// <summary>
/// 基于 SignalR 的 screencast 帧接收端，将帧通过 Hub 转发给客户端。<br/>
/// SignalR-based screencast frame sink that forwards frames to clients via the Hub.
/// </summary>
/// <param name="hub">SignalR Hub 上下文 / SignalR hub context used to send frames to clients.</param>
public sealed class SignalRScreencastFrameSink(IHubContext<BrowserHub, IBrowserClient> hub) : IScreencastFrameSink
{
    /// <summary>
    /// 发送一帧图像数据到所有已连接客户端。<br/>
    /// Send a frame of image data to all connected clients.
    /// </summary>
    /// <param name="data">Base64 或其他编码的帧数据 / base64 or encoded frame data.</param>
    /// <param name="width">可选帧宽度 / optional frame width.</param>
    /// <param name="height">可选帧高度 / optional frame height.</param>
    /// <param name="cancellationToken">取消令牌 / cancellation token.</param>
    /// <returns>表示异步操作的任务 / task representing the async operation.</returns>
    public Task SendFrameAsync(string data, int? width, int? height, CancellationToken cancellationToken = default) => hub.Clients.All.ReceiveFrame(data, width, height);

    /// <summary>
    /// 通知客户端投屏功能已被禁用。<br/>
    /// Notify clients that screencast has been disabled.
    /// </summary>
    /// <param name="cancellationToken">取消令牌 / cancellation token.</param>
    /// <returns>表示异步操作的任务 / task representing the async operation.</returns>
    public Task NotifyScreencastDisabledAsync(CancellationToken cancellationToken = default) => hub.Clients.All.ScreencastDisabled();
}
