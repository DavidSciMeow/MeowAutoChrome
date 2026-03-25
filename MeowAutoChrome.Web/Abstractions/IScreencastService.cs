using MeowAutoChrome.Web.Models;
using System.Threading.Tasks;
using System.Threading;

namespace MeowAutoChrome.Web.Abstractions;

public interface IScreencastService
{
    Task OnClientConnectedAsync();
    Task OnClientDisconnectedAsync();
    Task DispatchMouseEventAsync(MouseEventData data);
    Task DispatchKeyEventAsync(KeyEventData data);
}
