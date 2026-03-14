using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MeowAutoChrome.Contracts;

public static class BrowserPluginResultExtensions
{
    public static Task<BrowserPluginActionResult> Ok(this IBrowserPlugin plugin, string message, IReadOnlyDictionary<string, string?>? data = null)
        => Task.FromResult(plugin.OkResult(message, data));

    public static BrowserPluginActionResult OkResult(this IBrowserPlugin plugin, string message, IReadOnlyDictionary<string, string?>? data = null)
        => new(message, MergeDefaultData(plugin, data));

    private static Dictionary<string, string?> MergeDefaultData(IBrowserPlugin plugin, IReadOnlyDictionary<string, string?>? data)
    {
        ArgumentNullException.ThrowIfNull(plugin);

        var result = new Dictionary<string, string?>
        {
            ["state"] = plugin.State.ToString(),
        };

        if (data is null)
            return result;

        foreach (var pair in data)
            result[pair.Key] = pair.Value;

        return result;
    }
}
