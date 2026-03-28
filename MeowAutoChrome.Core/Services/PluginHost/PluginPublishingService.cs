using MeowAutoChrome.Core.Interface;

namespace MeowAutoChrome.Core.Services.PluginHost;

public sealed class PluginPublishingService
{
    private readonly IPluginOutputPublisher _publisher;

    public PluginPublishingService(IPluginOutputPublisher publisher) => _publisher = publisher;

    public Task PublishAsync(string pluginId, string targetId, string? message, IReadOnlyDictionary<string, string?>? data, bool openModal, string? connectionId, CancellationToken cancellationToken)
        => _publisher.PublishPluginOutputAsync(pluginId, targetId, message, data, openModal, connectionId, cancellationToken);
}
