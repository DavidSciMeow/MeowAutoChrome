using System;

namespace MeowAutoChrome.Contracts.Attributes;

/// <summary>
/// 浏览器插件输入特性，用于标记方法或参数为浏览器插件的输入项。
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Parameter, AllowMultiple = true, Inherited = false)]
public sealed class PInputAttribute : Attribute
{
    /// <summary>
    /// 浏览器插件输入特性的构造函数，接受一个标签参数，用于描述输入项的名称或用途。
    /// </summary>
    /// <param name="label">输入项的名称或用途</param>
    public PInputAttribute(string label)
    {
        Label = label;
    }
    /// <summary>
    /// 浏览器插件输入特性的构造函数，接受一个标签参数和一个描述参数，用于描述输入项的名称、用途和详细信息。
    /// </summary>
    /// <param name="label">输入项的名称或用途</param>
    /// <param name="description">输入项的详细描述</param>
    public PInputAttribute(string label, string description)
    {
        Label = label;
        Description = description;
    }
    /// <summary>
    /// 输入项的名称或用途
    /// </summary>
    public string Label { get; }
    /// <summary>
    /// 输入项的详细描述
    /// </summary>
    public string? Description { get; init; }
    /// <summary>
    /// 输入项的名称，默认为null，如果不为null，则表示输入项的名称，否则使用参数名作为输入项的名称。
    /// </summary>
    public string? Name { get; set; }
    /// <summary>
    /// 默认值，默认为null，如果不为null，则表示输入项的默认值，否则没有默认值。
    /// </summary>
    public string? DefaultValue { get; set; }
    /// <summary>
    /// 是否必填，默认为false，如果为true，则表示输入项是必填的，否则是可选的。
    /// </summary>
    public bool Required { get; set; }
    /// <summary>
    /// 输入类型，默认为"text"，表示输入项的类型，可以是"text"、"number"、"password"等常见的输入类型，也可以是自定义的输入类型，根据实际需求进行设置。
    /// </summary>
    public string InputType { get; set; } = "text";
}


