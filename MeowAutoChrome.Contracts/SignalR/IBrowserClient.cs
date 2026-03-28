namespace MeowAutoChrome.Contracts.SignalR
{
    // Minimal placeholder for SignalR client contract used by Hub generic parameter.
    // Real definition can be restored later; this keeps the solution compiling.
    public interface IBrowserClient
    {
        System.Threading.Tasks.Task ReceiveFrame(string data, int? width, int? height);
        System.Threading.Tasks.Task ScreencastDisabled();
    }
}
