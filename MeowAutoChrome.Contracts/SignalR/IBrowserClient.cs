using System;
using System.Threading.Tasks;

namespace MeowAutoChrome.Contracts.SignalR;

[Obsolete("IBrowserClient belongs to host SignalR surface. This interface will be moved to the Web/Core layer and plugins should not implement it.")]
public interface IBrowserClient
{
    Task ReceiveFrame(string data, int? width, int? height);
    Task ScreencastDisabled();
}
