using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MeowAutoChrome.Core.Services;
using Microsoft.AspNetCore.SignalR;
using MeowAutoChrome.Web.Hubs;

namespace MeowAutoChrome.Web.Services;

public sealed class SignalRPluginOutputPublisher(IHubContext<BrowserHub> hub) : IPluginOutputPublisher
{
    public Task PublishAsync(string pluginId, string targetId, string? message, IReadOnlyDictionary<string, string?>? data, bool openModal, string? connectionId, CancellationToken cancellationToken)
    {
        var payload = new Contracts.BrowserPlugin.BrowserPluginOutputUpdate(pluginId, targetId, message, data ?? new Dictionary<string, string?>(), openModal, DateTimeOffset.UtcNow);
        var clients = string.IsNullOrWhiteSpace(connectionId) ? hub.Clients.All : hub.Clients.Client(connectionId);
        return clients.SendAsync("ReceivePluginOutput", payload, cancellationToken);
    }
}
