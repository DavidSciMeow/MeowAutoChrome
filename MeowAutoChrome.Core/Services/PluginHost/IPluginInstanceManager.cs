using MeowAutoChrome.Core.Models;

namespace MeowAutoChrome.Core.Services.PluginHost;

public interface IPluginInstanceManager
{
    RuntimeBrowserPluginInstance GetOrCreateInstance(RuntimeBrowserPlugin plugin);
    void RemoveInstanceByPluginId(string pluginId);
    void EnsureFreshLifecycleToken(RuntimeBrowserPlugin plugin);
    void CancelLifecycle(RuntimeBrowserPlugin plugin);
}
