using System.Collections.Generic;

namespace MeowAutoChrome.Core.Models;

public sealed class PluginDiscoverySnapshot
{
    public IReadOnlyList<RuntimeBrowserPlugin> Plugins { get; }
    public IReadOnlyList<string> Errors { get; }
    public IReadOnlyList<MeowAutoChrome.Contracts.BrowserPlugin.BrowserPluginErrorDescriptor>? ErrorsDetailed { get; }

    public PluginDiscoverySnapshot(IReadOnlyList<RuntimeBrowserPlugin> plugins, IReadOnlyList<string> errors, IReadOnlyList<MeowAutoChrome.Contracts.BrowserPlugin.BrowserPluginErrorDescriptor>? errorsDetailed = null)
    {
        Plugins = plugins;
        Errors = errors;
        ErrorsDetailed = errorsDetailed;
    }
}
