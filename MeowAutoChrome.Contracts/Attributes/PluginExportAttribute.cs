using System;

namespace MeowAutoChrome.Contracts.Attributes;

/// <summary>
/// 标记要导出的插件类型或方法，携带导出 ID 与名称。<br/>
/// Attribute that marks a plugin type or method as exported, carrying an id and name.
/// </summary>
/// <param name="id">导出标识符。<br/>Export identifier.</param>
/// <param name="name">导出显示名称。<br/>Export display name.</param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false)]
public sealed class PluginExportAttribute(string id, string name) : Attribute
{
    /// <summary>
    /// 导出标识符。<br/>
    /// Export identifier.
    /// </summary>
    public string Id { get; } = id;
    /// <summary>
    /// 导出显示名称。<br/>
    /// Export display name.
    /// </summary>
    public string Name { get; } = name;
}
