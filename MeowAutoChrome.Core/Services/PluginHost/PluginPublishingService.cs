using MeowAutoChrome.Core.Interface;

namespace MeowAutoChrome.Core.Services.PluginHost;

/// <summary>
/// 插件发布服务：封装向外部发布插件输出的发布器接口调用。<br/>
/// Plugin publishing service that wraps the output publisher to forward plugin outputs.
/// </summary>
/// <remarks>
/// 创建发布服务并注入发布器。<br/>
/// Create the publishing service with the provided output publisher.
/// </remarks>
/// <param name="publisher">插件输出发布器 / plugin output publisher.</param>
public sealed class PluginPublishingService(IPluginOutputPublisher publisher)
{

    /// <summary>
    /// 将插件的输出（消息/数据）发布到订阅方或客户端。<br/>
    /// Publish plugin output (message/data) to subscribers or clients.
    /// </summary>
    public Task PublishAsync(string pluginId, string targetId, string? message, IReadOnlyDictionary<string, string?>? data, bool openModal, string? connectionId, CancellationToken cancellationToken) => publisher.PublishPluginOutputAsync(pluginId, targetId, message, data, openModal, connectionId, cancellationToken);
}
