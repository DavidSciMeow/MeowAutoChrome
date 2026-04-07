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

    public interface ILogClient
    {
        Task ReceiveLog(LogMessageDto entry);
    }
}
