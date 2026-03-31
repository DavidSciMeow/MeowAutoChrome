using System;

namespace MeowAutoChrome.Contracts.Attributes;

/// <summary>
/// 标注方法参数或属性为插件输入元数据（名称、标签、描述等）。<br/>
/// Attribute to annotate a parameter or property as plugin input metadata (name, label, description, etc.).
/// </summary>
[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property, AllowMultiple = true)]
public sealed class PInputAttribute : Attribute
{
    /// <summary>
    /// 输入参数的名称（键）。<br/>
    /// Name/key of the input parameter.
    /// </summary>
    public string? Name { get; set; }
    /// <summary>
    /// 显示标签。<br/>
    /// Display label.
    /// </summary>
    public string? Label { get; set; }
    /// <summary>
    /// 参数描述。<br/>
    /// Description of the parameter.
    /// </summary>
    public string? Description { get; set; }
    /// <summary>
    /// 默认值（字符串形式）。<br/>
    /// Default value (as string).
    /// </summary>
    public string? DefaultValue { get; set; }
    /// <summary>
    /// 是否必填。<br/>
    /// Whether the input is required.
    /// </summary>
    public bool Required { get; set; }
    /// <summary>
    /// 输入类型提示（例如 text、password 等）。<br/>
    /// Hint for input type (e.g., text, password).
    /// </summary>
    public string? InputType { get; set; }
}
