using System;

namespace MeowAutoChrome.Contracts.Attributes;

/// <summary>
/// 标记插件类型并携带元数据（id、name、description）。<br/>
/// Attribute that marks a plugin type and carries metadata (id, name, description).
/// </summary>
/// <param name="id">插件 ID。<br/>Plugin identifier.</param>
/// <param name="name">插件名称。<br/>Plugin name.</param>
/// <param name="description">可选的插件描述。<br/>Optional plugin description.</param>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class PluginAttribute(string id, string name, string? description = null) : Attribute
{
    /// <summary>
    /// 插件 ID。<br/>
    /// Plugin identifier.
    /// </summary>
    public string Id { get; } = id;
    /// <summary>
    /// 插件名称。<br/>
    /// Plugin name.
    /// </summary>
    public string Name { get; } = name;
    /// <summary>
    /// 可选的插件描述。<br/>
    /// Optional plugin description.
    /// </summary>
    public string? Description { get; set; } = description;
}
