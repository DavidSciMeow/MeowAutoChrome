using System.Collections.Generic;
using System.Threading;
using Microsoft.Playwright;

namespace MeowAutoChrome.Contracts;

public interface IHostContext
{
    IBrowserContext BrowserContext { get; }
    IPage? ActivePage { get; }
    string BrowserInstanceId { get; }
    IBrowserInstanceManager BrowserInstanceManager { get; }
    IReadOnlyDictionary<string, string?> Arguments { get; }
    CancellationToken CancellationToken { get; }
}
