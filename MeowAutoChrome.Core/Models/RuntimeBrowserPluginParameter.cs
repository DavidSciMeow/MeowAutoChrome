namespace MeowAutoChrome.Core.Models;

/// <summary>
/// 运行时代码中描述插件参数的模型。<br/>
/// Model describing a plugin parameter at runtime.
/// </summary>
public sealed class RuntimeBrowserPluginParameter
{
    /// <summary>
    /// 参数名（键）。<br/>
    /// Parameter name (key).
    /// </summary>
    public string Name { get; init; } = string.Empty;
    /// <summary>
    /// 显示标签。<br/>
    /// Display label.
    /// </summary>
    public string Label { get; init; } = string.Empty;
    /// <summary>
    /// 参数描述（可空）。<br/>
    /// Description of the parameter (nullable).
    /// </summary>
    public string? Description { get; init; }
    /// <summary>
    /// 默认值。<br/>
    /// Default value.
    /// </summary>
    public string? DefaultValue { get; init; }
    /// <summary>
    /// 是否必填。<br/>
    /// Whether this parameter is required.
    /// </summary>
    public bool Required { get; init; }
    /// <summary>
    /// 输入类型（例如 text、password）。<br/>
    /// Input type (e.g., text, password).
    /// </summary>
    public string InputType { get; init; } = "text";
    /// <summary>
    /// 当输入类型为 textarea 时的可见行数。<br/>
    /// Visible row count when the input type is textarea.
    /// </summary>
    public int? Rows { get; init; }
    /// <summary>
    /// 可选值列表（Value, Label）。<br/>
    /// Option list of (Value, Label) pairs.
    /// </summary>
    public IReadOnlyList<(string Value, string Label)> Options { get; init; } = [];
}
