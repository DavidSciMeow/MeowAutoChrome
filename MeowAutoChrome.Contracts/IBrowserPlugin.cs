using System.Threading.Tasks;

namespace MeowAutoChrome.Contracts;

public interface IBrowserPlugin
{
    BrowserPluginState State { get; }
    bool SupportsPause { get; }
    IHostContext? HostContext { get; set; }
    Task<BrowserPluginActionResult> StartAsync();
    Task<BrowserPluginActionResult> StopAsync();
    Task<BrowserPluginActionResult> PauseAsync();
    Task<BrowserPluginActionResult> ResumeAsync();
}


