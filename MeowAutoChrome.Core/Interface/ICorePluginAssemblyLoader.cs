using System.Reflection;

namespace MeowAutoChrome.Core.Interface;

/// <summary>
/// Core-facing abstraction for plugin assembly loading to avoid mutual namespace dependency
/// between PluginHost and PluginDiscovery.
/// </summary>
public interface ICorePluginAssemblyLoader
{
    Assembly? Load(string pluginPath, List<string> errors);
    void Unload(string pluginPath);
    void RegisterPlugins(string pluginPath, IEnumerable<string> pluginIds);
    void UnregisterPlugins(string pluginPath);
    string? GetAssemblyPathForPluginId(string pluginId);
}
