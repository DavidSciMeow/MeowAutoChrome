using System.Collections.Generic;
using System.Threading;
using Microsoft.Playwright;

namespace MeowAutoChrome.Contracts;

public sealed record BrowserPluginHostContext(
    IBrowserContext BrowserContext,
    IPage? ActivePage,
    IReadOnlyDictionary<string, string?> Arguments,
    CancellationToken CancellationToken) : IHostContext;
