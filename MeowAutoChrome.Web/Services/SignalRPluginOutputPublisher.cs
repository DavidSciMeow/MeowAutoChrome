using Microsoft.AspNetCore.SignalR;
using MeowAutoChrome.Web.Hubs;
using MeowAutoChrome.Core.Interface;

namespace MeowAutoChrome.Web.Services;

public sealed class SignalRPluginOutputPublisher : IPluginOutputPublisher
{
    private readonly IHubContext<BrowserHub> _hub;
    public SignalRPluginOutputPublisher(IHubContext<BrowserHub> hub) => _hub = hub;

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
