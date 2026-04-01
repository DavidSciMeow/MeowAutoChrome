using Microsoft.AspNetCore.SignalR;
using MeowAutoChrome.WebAPI.Hubs;
using MeowAutoChrome.Core.Interface;

namespace MeowAutoChrome.WebAPI.Services;

/// <summary>
/// 基于 SignalR 的插件输出发布器。<br/>
/// SignalR-based publisher for plugin output messages.
/// </summary>
public sealed class SignalRPluginOutputPublisher : IPluginOutputPublisher
{
    private readonly IHubContext<BrowserHub> _hub;

    /// <summary>
    /// 初始化插件输出发布器。<br/>
    /// Initialize the plugin output publisher.
    /// </summary>
    /// <param name="hub">BrowserHub 上下文。<br/>BrowserHub context.</param>
    public SignalRPluginOutputPublisher(IHubContext<BrowserHub> hub) => _hub = hub;

    /// <summary>
    /// 发布插件输出到所有客户端或指定客户端。<br/>
    /// Publish plugin output to all clients or to a specified client.
    /// </summary>
    public Task PublishPluginOutputAsync(string pluginId, string targetId, string? message, IReadOnlyDictionary<string, string?>? data, bool openModal, string? connectionId, CancellationToken cancellationToken)
    {
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
