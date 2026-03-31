namespace MeowAutoChrome.Contracts.SignalR
{
    /// <summary>
    /// SignalR 客户端契约的最小占位定义（供 Hub 的泛型参数使用）。<br/>
    /// Minimal placeholder for the SignalR client contract used by the Hub generic parameter.
    /// </summary>
    public interface IBrowserClient
    {
        /// <summary>
        /// 接收一帧编码数据（例如屏幕抓取帧）。<br/>
        /// Receive an encoded frame (for example, a screencast frame).
        /// </summary>
        /// <param name="data">帧数据字符串。<br/>Frame data string.</param>
        /// <param name="width">可选的宽度。<br/>Optional width.</param>
        /// <param name="height">可选的高度。<br/>Optional height.</param>
        Task ReceiveFrame(string data, int? width, int? height);
        /// <summary>
        /// 通知客户端屏幕抓取已禁用。<br/>
        /// Notify the client that screencast has been disabled.
        /// </summary>
        Task ScreencastDisabled();
    }
}
