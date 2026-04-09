namespace MeowAutoChrome.Core.Interface;

/// <summary>
/// 插件输出发布者接口，负责将插件产生的输出发送到目标（例如 UI 或 SignalR）。<br/>
/// Interface responsible for publishing plugin output to targets such as UI or SignalR.
/// </summary>
public interface IPluginOutputPublisher
{
    /// <summary>
    /// 发布插件输出到指定目标。<br/>
    /// Publish plugin output to the specified target.
    /// </summary>
    /// <param name="pluginId">插件 ID。<br/>Plugin id.</param>
    /// <param name="targetId">目标 ID（例如 UI 区域或连接标识）。<br/>Target id (e.g., UI area or connection identifier).</param>
    /// <param name="message">可选的文本消息。<br/>Optional text message.</param>
    /// <param name="data">可选的键值数据。<br/>Optional key/value data.</param>
    /// <param name="toastRequested">是否额外推送 toast。<br/>Whether a toast should be pushed in addition to storing the message.</param>
    /// <param name="connectionId">可选的连接 ID（如 SignalR 连接）。<br/>Optional connection id (e.g., SignalR connection).</param>
    /// <param name="cancellationToken">取消令牌。<br/>Cancellation token.</param>
    Task PublishPluginOutputAsync(string pluginId, string targetId, string? message, IReadOnlyDictionary<string, string?>? data, bool toastRequested, string? connectionId, CancellationToken cancellationToken);
}
