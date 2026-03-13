using System.Collections.Generic;
using System.Threading;
using Microsoft.Playwright;

namespace MeowAutoChrome.Contracts;

public interface IHostContext
{
    IBrowserContext BrowserContext { get; }
    IPage? ActivePage { get; }
    IReadOnlyDictionary<string, string?> Arguments { get; }
    CancellationToken CancellationToken { get; }
}
