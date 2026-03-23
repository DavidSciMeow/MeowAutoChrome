using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MeowAutoChrome.Core.Services;
using Microsoft.AspNetCore.SignalR;
using MeowAutoChrome.Web.Hubs;

namespace MeowAutoChrome.Web.Services;

public sealed class SignalRScreencastFrameSink(IHubContext<BrowserHub> hub) : IScreencastFrameSink
{
    public Task SendFrameAsync(string data, int? width, int? height, CancellationToken cancellationToken = default)
    {
        return hub.Clients.All.SendAsync("ReceiveFrame", new { data, width, height }, cancellationToken);
    }

    public Task NotifyScreencastDisabledAsync(CancellationToken cancellationToken = default)
    {
        return hub.Clients.All.SendAsync("ScreencastDisabled", cancellationToken);
    }
}
