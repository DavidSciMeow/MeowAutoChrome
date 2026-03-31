namespace MeowAutoChrome.Core.Models;

/// <summary>
/// 运行时的浏览器插件描述，包含反射类型与已发现的控制与动作。<br/>
/// Runtime description of a browser plugin including reflection type and discovered controls/actions.
/// </summary>
/// <param name="id">插件 ID / plugin id.</param>
/// <param name="name">插件名称 / plugin name.</param>
/// <param name="description">插件描述（可空）/ plugin description (nullable).</param>
/// <param name="type">插件实现类型（反射）/ plugin implementation type (reflection).</param>
/// <param name="controls">发现的控制列表 / discovered controls list.</param>
/// <param name="actions">发现的动作列表 / discovered actions list.</param>
public sealed class RuntimeBrowserPlugin(string id, string name, string? description, Type type, List<RuntimeBrowserPluginControl> controls, List<RuntimeBrowserPluginAction> actions)
{
    /// <summary>
    /// 插件 ID。<br/>
    /// Plugin id.
    /// </summary>
    public string Id { get; } = id;
    /// <summary>
    /// 插件名称。<br/>
    /// Plugin name.
    /// </summary>
    public string Name { get; } = name;
    /// <summary>
    /// 插件描述（可空）。<br/>
    /// Plugin description (nullable).
    /// </summary>
    public string? Description { get; } = description;
    /// <summary>
    /// 插件实现类型（反射）。<br/>
    /// Plugin implementation type (reflection).
    /// </summary>
    public Type Type { get; } = type;
    /// <summary>
    /// 插件的控制列表。<br/>
    /// List of controls for the plugin.
    /// </summary>
    public List<RuntimeBrowserPluginControl> Controls { get; } = controls;
    /// <summary>
    /// 插件的动作列表。<br/>
    /// List of actions for the plugin.
    /// </summary>
    public List<RuntimeBrowserPluginAction> Actions { get; } = actions;
}
