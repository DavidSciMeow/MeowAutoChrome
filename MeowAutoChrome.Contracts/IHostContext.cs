using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace MeowAutoChrome.Contracts;

public interface IHostContext
{
    IBrowserContext BrowserContext { get; }
    IPage? ActivePage { get; }
    string BrowserInstanceId { get; }
    IBrowserInstanceManager BrowserInstanceManager { get; }
    IReadOnlyDictionary<string, string?> Arguments { get; }
    string PluginId { get; }
    string TargetId { get; }
    CancellationToken CancellationToken { get; }
    Task PublishUpdateAsync(string? message, IReadOnlyDictionary<string, string?>? data = null, bool openModal = true);
}
