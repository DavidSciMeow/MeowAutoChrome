using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MeowAutoChrome.Contracts;

public interface IBrowserPlugin
{
    BrowserPluginState State { get; }
    bool SupportsPause { get; }
    Task<BrowserPluginActionResult> StartAsync(IReadOnlyDictionary<string, string?> arguments, IBrowserPluginContext context, CancellationToken cancellationToken = default);
    Task<BrowserPluginActionResult> StopAsync(IBrowserPluginContext context, CancellationToken cancellationToken = default);
    Task<BrowserPluginActionResult> PauseAsync(IBrowserPluginContext context, CancellationToken cancellationToken = default);
    Task<BrowserPluginActionResult> ResumeAsync(IBrowserPluginContext context, CancellationToken cancellationToken = default);
}


