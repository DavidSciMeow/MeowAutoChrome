using System.Threading.Tasks;

namespace MeowAutoChrome.Contracts.SignalR;

public interface IBrowserClient
{
    Task ReceiveFrame(string data, int? width, int? height);
    Task ScreencastDisabled();
}
