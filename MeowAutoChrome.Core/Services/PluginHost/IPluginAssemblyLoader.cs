using System.Reflection;

namespace MeowAutoChrome.Core.Services.PluginHost;

public interface IPluginAssemblyLoader : Interface.ICorePluginAssemblyLoader
{
    Assembly? Load(string pluginPath, List<string> errors);
    void Unload(string pluginPath);
    void RegisterPlugins(string pluginPath, IEnumerable<string> pluginIds);
    void UnregisterPlugins(string pluginPath);
    string? GetAssemblyPathForPluginId(string pluginId);
}
