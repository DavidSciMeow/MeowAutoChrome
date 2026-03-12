using System.Collections.Generic;

namespace MeowAutoChrome.Contracts;

public static class BrowserPluginApi
{
    public const string CurrentVersion = "1.0";
}

public static class BrowserPluginCapabilities
{
    public const string PageTitle = "browser.page.title";
    public const string CurrentUrl = "browser.page.url";
}

public sealed record BrowserPluginActionResult(string? Message, IReadOnlyDictionary<string, string?>? Data = null);


