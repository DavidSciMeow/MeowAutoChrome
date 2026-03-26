using MeowAutoChrome.Contracts.BrowserPlugin;

namespace MeowAutoChrome.Core.Models;

public sealed class PluginDiscoverySnapshot(IReadOnlyList<RuntimeBrowserPlugin> plugins, IReadOnlyList<string> errors, IReadOnlyList<MeowAutoChrome.Contracts.BrowserPlugin.BrowserPluginErrorDescriptor>? errorsDetailed = null)
{
    public IReadOnlyList<RuntimeBrowserPlugin> Plugins { get; } = plugins;
    public IReadOnlyList<string> Errors { get; } = errors;
    public IReadOnlyList<BrowserPluginErrorDescriptor>? ErrorsDetailed { get; } = errorsDetailed;
}
