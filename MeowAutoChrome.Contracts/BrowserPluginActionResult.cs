using System.Collections.Generic;

namespace MeowAutoChrome.Contracts;

public sealed record BrowserPluginActionResult(string? Message, IReadOnlyDictionary<string, string?>? Data = null);


