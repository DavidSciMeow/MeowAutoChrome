using MeowAutoChrome.Core.Models;

namespace MeowAutoChrome.Core.Services.PluginDiscovery;

public interface IPluginDiscoveryService
{
    string PluginRootPath { get; }
    void EnsurePluginDirectoryExists();
    IEnumerable<string> EnumeratePluginAssemblies();
    PluginDiscoverySnapshot DiscoverAll(Interface.ICorePluginAssemblyLoader assemblyLoader);
    (List<RuntimeBrowserPlugin> Plugins, List<string> Errors, List<BrowserPluginErrorDescriptor> ErrorsDetailed) DiscoverFromAssembly(string pluginPath, Interface.ICorePluginAssemblyLoader assemblyLoader);
}
