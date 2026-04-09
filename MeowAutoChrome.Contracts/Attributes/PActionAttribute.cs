using System;

namespace MeowAutoChrome.Contracts.Attributes;

/// <summary>
/// 标记方法为插件动作，并携带可选的动作元数据（Id/Name/Description）。<br/>
/// Attribute that marks a method as a plugin action and carries optional metadata (Id/Name/Description).
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class PActionAttribute : Attribute
{
    /// <summary>
    /// 动作标识，可选。<br/>
    /// Optional identifier for the action.
    /// </summary>
    public string? Id { get; set; }
    /// <summary>
    /// 动作名称，用于展示。<br/>
    /// Display name of the action.
    /// </summary>
    public string? Name { get; set; }
    /// <summary>
    /// 动作描述。<br/>
    /// Description of the action.
    /// </summary>
    public string? Description { get; set; }
}
