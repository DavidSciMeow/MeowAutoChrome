namespace MeowAutoChrome.Core.Services;

/// <summary>
/// 屏幕投射帧接收端接口：负责接收编码后的帧并将其发送到客户端或存储层。<br/>
/// Interface for a screencast frame sink which receives encoded frames and forwards them to clients or storage.
/// </summary>
public interface IScreencastFrameSink
{
    /// <summary>
    /// 发送一帧编码数据到接收端。<br/>
    /// Send an encoded frame to the sink.
    /// </summary>
    /// <param name="data">Base64 或其他编码的图像数据 / encoded image data (base64 or similar).</param>
    /// <param name="width">可选的帧宽度 / optional width of the frame.</param>
    /// <param name="height">可选的帧高度 / optional height of the frame.</param>
    /// <param name="cancellationToken">取消令牌 / cancellation token.</param>
    Task SendFrameAsync(string data, int? width, int? height, CancellationToken cancellationToken = default);

    /// <summary>
    /// 通知接收端屏幕投射已被禁用。<br/>
    /// Notify the sink that screencast has been disabled.
    /// </summary>
    /// <param name="cancellationToken">取消令牌 / cancellation token.</param>
    Task NotifyScreencastDisabledAsync(CancellationToken cancellationToken = default);
}
