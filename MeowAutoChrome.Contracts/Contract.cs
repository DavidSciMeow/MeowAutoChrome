using System.Collections.Generic;

namespace MeowAutoChrome.Contracts;

public static class BrowserPluginApi
{
    public const string CurrentVersion = "1.0";
}

public sealed record BrowserPluginActionResult(string? Message, IReadOnlyDictionary<string, string?>? Data = null);


