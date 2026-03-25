using System;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace MeowAutoChrome.Core.Services.PluginHost;

/// <summary>
/// AssemblyLoadContext for plugin isolation that resolves dependencies using AssemblyDependencyResolver.
/// It is collectible so plugin assemblies can be unloaded when no longer referenced.
/// </summary>
public sealed class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    public PluginLoadContext(string pluginPath) : base(Path.GetFileNameWithoutExtension(pluginPath), isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Try to resolve assembly using dependency resolver (searches plugin folder and runtime deps)
        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (!string.IsNullOrEmpty(assemblyPath) && File.Exists(assemblyPath))
        {
            try
            {
                return LoadFromAssemblyPath(assemblyPath);
            }
            catch
            {
                return null;
            }
        }

        // Fallback to default context
        return null;
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        if (!string.IsNullOrEmpty(libraryPath) && File.Exists(libraryPath))
            return LoadUnmanagedDllFromPath(libraryPath);

        return IntPtr.Zero;
    }
}
