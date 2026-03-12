using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace MeowAutoChrome.Contracts;

public interface IBrowserPlugin
{
    BrowserPluginState State { get; }
    bool SupportsPause { get; }
    Task<BrowserPluginActionResult> StartAsync(IReadOnlyDictionary<string, string?> arguments, IBrowserContext browserContext, IPage? activePage, CancellationToken cancellationToken = default);
    Task<BrowserPluginActionResult> StopAsync(IBrowserContext browserContext, IPage? activePage, CancellationToken cancellationToken = default);
    Task<BrowserPluginActionResult> PauseAsync(IBrowserContext browserContext, IPage? activePage, CancellationToken cancellationToken = default);
    Task<BrowserPluginActionResult> ResumeAsync(IBrowserContext browserContext, IPage? activePage, CancellationToken cancellationToken = default);
}


