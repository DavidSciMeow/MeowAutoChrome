namespace MeowAutoChrome.Contracts.SignalR
{
    /// <summary>
    /// SignalR 客户端契约：接收新增日志条目。
    /// Client contract for receiving log entries via SignalR.
    /// </summary>
    public record LogMessageDto(
        string TimestampText,
        string LevelText,
        string FilterLevel,
        string Category,
        string Message);

    /// <summary>
    /// SignalR 日志客户端契约。<br/>
    /// SignalR client contract for log streaming.
    /// </summary>
    public interface ILogClient
    {
        /// <summary>
        /// 接收一条新增日志。<br/>
        /// Receive a newly appended log entry.
        /// </summary>
        /// <param name="entry">日志条目。<br/>Log entry payload.</param>
        Task ReceiveLog(LogMessageDto entry);
    }
}
