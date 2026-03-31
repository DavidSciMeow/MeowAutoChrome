using Microsoft.AspNetCore.SignalR;
using MeowAutoChrome.Web.Hubs;
using MeowAutoChrome.Core.Interface;

namespace MeowAutoChrome.Web.Services;

/// <summary>
/// 基于 SignalR 的插件输出发布器，用于将插件消息推送到浏览器客户端。<br/>
/// SignalR-based publisher for plugin output, sending plugin messages to browser clients.
/// </summary>
public sealed class SignalRPluginOutputPublisher : IPluginOutputPublisher
{
    private readonly IHubContext<BrowserHub> _hub;
    /// <summary>
    /// 创建 SignalRPluginOutputPublisher。<br/>
    /// Create a SignalRPluginOutputPublisher.
    /// </summary>
    /// <param name="hub">SignalR 的 Hub 上下文 / SignalR hub context.</param>
    public SignalRPluginOutputPublisher(IHubContext<BrowserHub> hub) => _hub = hub;

    /// <summary>
    /// 将插件输出发送到客户端（可选择单个连接或所有连接）。<br/>
    /// Publish plugin output to clients (optionally to a single connection or all clients).
    /// </summary>
    /// <param name="pluginId">插件 Id / plugin id.</param>
    /// <param name="targetId">目标 Id（UI 上的目标）/ target id in the UI.</param>
    /// <param name="message">可选消息文本 / optional message text.</param>
    /// <param name="data">可选键值数据 / optional key/value data.</param>
    /// <param name="openModal">指示是否打开模态窗口 / whether to open a modal on the client.</param>
    /// <param name="connectionId">可选连接 Id，仅向该连接发送 / optional connection id to target a single client.</param>
    /// <param name="cancellationToken">取消令牌 / cancellation token.</param>
    /// <returns>表示异步操作的任务 / task representing the async operation.</returns>
    public Task PublishPluginOutputAsync(string pluginId, string targetId, string? message, IReadOnlyDictionary<string, string?>? data, bool openModal, string? connectionId, CancellationToken cancellationToken)
    {
        // Use a lightweight anonymous payload to avoid referencing Core model types in Web layer.
        var payload = new
        {
            PluginId = pluginId,
            TargetId = targetId,
            Message = message,
            Data = data ?? new Dictionary<string, string?>(),
            OpenModal = openModal,
            TimestampUtc = DateTimeOffset.UtcNow
        };

        var clients = string.IsNullOrWhiteSpace(connectionId) ? _hub.Clients.All : _hub.Clients.Client(connectionId);
        return clients.SendAsync("ReceivePluginOutput", payload, cancellationToken);
    }
}
