using System.Collections.Generic;
using System.Reflection;

namespace MeowAutoChrome.Core.Services.PluginDiscovery;

public sealed class PluginDiscoveryService
{
    private readonly string _pluginRootPath;

    public PluginDiscoveryService(string? pluginRootPath = null)
    {
        _pluginRootPath = pluginRootPath ?? Path.Combine(AppContext.BaseDirectory, "Plugins");
    }

    public string PluginRootPath => _pluginRootPath;

    public void EnsurePluginDirectoryExists() => Directory.CreateDirectory(_pluginRootPath);

    // For now provide a minimal discovery that returns DLL paths. Detailed discovery implemented in BrowserPluginHostCore.
    public IEnumerable<string> EnumeratePluginAssemblies() => Directory.EnumerateFiles(_pluginRootPath, "*.dll", SearchOption.AllDirectories);
}
