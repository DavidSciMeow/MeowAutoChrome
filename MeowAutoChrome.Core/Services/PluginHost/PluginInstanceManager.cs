using System;
using System.Collections.Generic;
using MeowAutoChrome.Core.Models;
using MeowAutoChrome.Contracts.Interface;

namespace MeowAutoChrome.Core.Services.PluginHost;

/// <summary>
/// Manages runtime plugin instances and their lifecycle tokens.
/// Extracted from BrowserPluginHostCore to reduce that type's size.
/// </summary>
public sealed class PluginInstanceManager : IPluginInstanceManager
{
    private readonly Dictionary<string, RuntimeBrowserPluginInstance> _instances = new(StringComparer.OrdinalIgnoreCase);

    public RuntimeBrowserPluginInstance GetOrCreateInstance(RuntimeBrowserPlugin plugin)
    {
        lock (_instances)
        {
            if (_instances.TryGetValue(plugin.Id, out var current) && current.Type == plugin.Type)
                return current;

            if (Activator.CreateInstance(plugin.Type) is not IPlugin instance)
                throw new InvalidOperationException($"无法创建插件实例：{plugin.Type.FullName}");

            current = new RuntimeBrowserPluginInstance(plugin.Type, instance);
            _instances[plugin.Id] = current;
            return current;
        }
    }

    public RuntimeBrowserPluginInstance? GetInstanceIfExists(RuntimeBrowserPlugin plugin)
    {
        lock (_instances)
        {
            if (_instances.TryGetValue(plugin.Id, out var current) && current.Type == plugin.Type)
                return current;
            return null;
        }
    }

    public void EnsureFreshLifecycleToken(RuntimeBrowserPlugin plugin)
    {
        var inst = GetInstanceIfExists(plugin);
        if (inst is null) return;
        inst.EnsureFreshLifecycleToken();
    }

    public void CancelLifecycle(RuntimeBrowserPlugin plugin)
    {
        var inst = GetInstanceIfExists(plugin);
        if (inst is null) return;
        inst.CancelLifecycle();
    }

    public void RemoveInstanceByPluginId(string pluginId)
    {
        lock (_instances)
        {
            _instances.Remove(pluginId);
        }
    }
}
