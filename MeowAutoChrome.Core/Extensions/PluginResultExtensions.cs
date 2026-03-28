using MeowAutoChrome.Contracts;

namespace MeowAutoChrome.Core.Extensions;

/// <summary>
/// Core implementation of plugin result extension helpers migrated from Contracts.
/// </summary>
public static class PluginResultExtensions
{
    public static Task<IResult> Ok(this IPlugin plugin, string message, IReadOnlyDictionary<string, string?>? data = null) => Task.FromResult(plugin.OkResult(message, data));
    public static IResult OkResult(this IPlugin plugin, string message, IReadOnlyDictionary<string, string?>? data = null) => Result.Ok(MergeDefaultData(plugin, data));

    private static Dictionary<string, string?> MergeDefaultData(IPlugin plugin, IReadOnlyDictionary<string, string?>? data)
    {
        ArgumentNullException.ThrowIfNull(plugin);
        var result = new Dictionary<string, string?>
        {
            ["state"] = plugin.State.ToString(),
        };

        if (data is null) return result;
        foreach (var pair in data) result[pair.Key] = pair.Value;
        return result;
    }
}
