using MeowAutoChrome.Web.Models;
using MeowAutoChrome.Web.Services;
using MeowAutoChrome.Web.Warpper;
using Microsoft.AspNetCore.SignalR;

namespace MeowAutoChrome.Web.Hubs;

public class BrowserHub(PlayWrightWarpper client, ScreencastService screencast) : Hub
{
    public override async Task OnConnectedAsync()
    {
        await screencast.OnClientConnectedAsync();
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await screencast.OnClientDisconnectedAsync();
        await base.OnDisconnectedAsync(exception);
    }

    public async Task SendMouseEvent(MouseEventData data)
        => await screencast.DispatchMouseEventAsync(data);

    public async Task SendKeyEvent(KeyEventData data)
        => await screencast.DispatchKeyEventAsync(data);
}



