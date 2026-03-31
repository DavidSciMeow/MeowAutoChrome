namespace MeowAutoChrome.Core.Models;

/// <summary>
/// 插件发现快照，包含发现的运行时插件与错误信息。<br/>
/// Snapshot of plugin discovery containing runtime plugins and error information.
/// </summary>
/// <param name="plugins">发现到的运行时插件列表 / discovered runtime plugins.</param>
/// <param name="errors">简要错误信息列表 / brief list of error messages.</param>
/// <param name="errorsDetailed">详细错误描述列表（可空）/ detailed error descriptors (nullable).</param>
public sealed class PluginDiscoverySnapshot(IReadOnlyList<RuntimeBrowserPlugin> plugins, IReadOnlyList<string> errors, IReadOnlyList<BrowserPluginErrorDescriptor>? errorsDetailed = null)
{
    /// <summary>
    /// 发现到的运行时插件列表。<br/>
    /// Discovered runtime plugins.
    /// </summary>
    public IReadOnlyList<RuntimeBrowserPlugin> Plugins { get; } = plugins;
    /// <summary>
    /// 简要错误信息列表。<br/>
    /// Brief list of errors.
    /// </summary>
    public IReadOnlyList<string> Errors { get; } = errors;
    /// <summary>
    /// 详细错误描述列表（可空）。<br/>
    /// Detailed error descriptors (nullable).
    /// </summary>
    public IReadOnlyList<BrowserPluginErrorDescriptor>? ErrorsDetailed { get; } = errorsDetailed;
}
