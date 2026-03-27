using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MeowAutoChrome.Core.Services;
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
        var payload = new MeowAutoChrome.Core.Models.BrowserPluginOutputUpdate(pluginId, targetId, message, data ?? new Dictionary<string, string?>(), openModal, DateTimeOffset.UtcNow);
        var clients = string.IsNullOrWhiteSpace(connectionId) ? _hub.Clients.All : _hub.Clients.Client(connectionId);
        return clients.SendAsync("ReceivePluginOutput", payload, cancellationToken);
    }
}
