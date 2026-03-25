using System.Collections.Generic;
using System.Reflection;
using MeowAutoChrome.Core.Models;

namespace MeowAutoChrome.Core.Services.PluginDiscovery;

public interface IPluginDiscoveryService
{
    string PluginRootPath { get; }
    void EnsurePluginDirectoryExists();
    IEnumerable<string> EnumeratePluginAssemblies();
    PluginDiscoverySnapshot DiscoverAll(MeowAutoChrome.Core.Services.PluginHost.IPluginAssemblyLoader assemblyLoader);
    (List<RuntimeBrowserPlugin> Plugins, List<string> Errors, List<MeowAutoChrome.Contracts.BrowserPlugin.BrowserPluginErrorDescriptor> ErrorsDetailed) DiscoverFromAssembly(string pluginPath, MeowAutoChrome.Core.Services.PluginHost.IPluginAssemblyLoader assemblyLoader);
}
