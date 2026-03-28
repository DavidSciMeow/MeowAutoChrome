using MeowAutoChrome.Core.Services;
using Microsoft.AspNetCore.SignalR;
using MeowAutoChrome.Web.Hubs;
using MeowAutoChrome.Contracts.SignalR;

namespace MeowAutoChrome.Web.Services;

public sealed class SignalRScreencastFrameSink(IHubContext<BrowserHub, IBrowserClient> hub) : IScreencastFrameSink
{
    public Task SendFrameAsync(string data, int? width, int? height, CancellationToken cancellationToken = default)
    {
        return hub.Clients.All.ReceiveFrame(data, width, height);
    }

    public Task NotifyScreencastDisabledAsync(CancellationToken cancellationToken = default)
    {
        return hub.Clients.All.ScreencastDisabled();
    }
}
