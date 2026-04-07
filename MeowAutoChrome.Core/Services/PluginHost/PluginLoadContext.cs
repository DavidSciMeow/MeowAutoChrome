using System.Reflection;
using System.Runtime.Loader;

namespace MeowAutoChrome.Core.Services.PluginHost;

/// <summary>
/// 用于插件隔离的 AssemblyLoadContext，使用 <see cref="AssemblyDependencyResolver"/> 解析依赖。
/// 可回收（collectible），以便在不再引用时卸载插件程序集。<br/>
/// AssemblyLoadContext for plugin isolation that resolves dependencies using <see cref="AssemblyDependencyResolver"/>. It is collectible so plugin assemblies can be unloaded when no longer referenced.
/// </summary>
/// <remarks>
/// 创建一个基于插件路径的可回收加载上下文。<br/>
/// Create a collectible load context based on the plugin path.
/// </remarks>
/// <param name="pluginPath">插件程序集文件路径 / plugin assembly file path.</param>
public sealed class PluginLoadContext(string pluginPath) : AssemblyLoadContext(Path.GetFileNameWithoutExtension(pluginPath), isCollectible: true)
{
    private readonly AssemblyDependencyResolver _resolver = new(pluginPath);

    /// <summary>
    /// 解析并加载托管程序集（由依赖解析器尝试定位路径）。<br/>
    /// Resolve and load a managed assembly using the dependency resolver if possible.
    /// </summary>
    /// <param name="assemblyName">要加载的程序集名称 / assembly name to load.</param>
    /// <returns>已加载的 Assembly 或 null（无法解析时）。</returns>
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

    /// <summary>
    /// 解析并加载非托管库，返回本机库句柄或 IntPtr.Zero。<br/>
    /// Resolve and load an unmanaged library, returning the native library handle or IntPtr.Zero.
    /// </summary>
    /// <param name="unmanagedDllName">非托管库名 / unmanaged dll name.</param>
    /// <returns>本机库句柄或 IntPtr.Zero / native library handle or IntPtr.Zero.</returns>
    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        if (!string.IsNullOrEmpty(libraryPath) && File.Exists(libraryPath)) return LoadUnmanagedDllFromPath(libraryPath);
        return IntPtr.Zero;
    }
}
