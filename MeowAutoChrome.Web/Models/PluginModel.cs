namespace MeowAutoChrome.Web.Models;

/// <summary>
/// 表示插件动作参数中的一个可选项。
/// </summary>
/// <param name="Value">可选项值。</param>
/// <param name="Label">可选项显示名称。</param>
public sealed record BrowserPluginActionParameterOptionDescriptor(string Value, string Label);

/// <summary>
/// 描述插件动作可接收的参数定义。
/// </summary>
/// <param name="Name">参数名称。</param>
/// <param name="Label">参数显示名称。</param>
/// <param name="Description">参数说明。</param>
/// <param name="DefaultValue">参数默认值。</param>
/// <param name="Required">是否为必填参数。</param>
/// <param name="InputType">参数输入类型。</param>
/// <param name="Options">参数可选项集合。</param>
public sealed record BrowserPluginActionParameterDescriptor(
    string Name,
    string Label,
    string? Description,
    string? DefaultValue,
    bool Required,
    string InputType,
    IReadOnlyList<BrowserPluginActionParameterOptionDescriptor> Options);

/// <summary>
/// 描述插件控制命令及其参数。
/// </summary>
/// <param name="Command">控制命令标识。</param>
/// <param name="Name">控制命令名称。</param>
/// <param name="Description">控制命令说明。</param>
/// <param name="Parameters">控制命令参数集合。</param>
public sealed record BrowserPluginControlDescriptor(
    string Command,
    string Name,
    string? Description,
    IReadOnlyList<BrowserPluginActionParameterDescriptor> Parameters);

/// <summary>
/// 描述插件函数及其参数。
/// </summary>
/// <param name="Id">函数标识。</param>
/// <param name="Name">函数名称。</param>
/// <param name="Description">函数说明。</param>
/// <param name="Parameters">函数参数集合。</param>
public sealed record BrowserPluginFunctionDescriptor(string Id, string Name, string? Description, IReadOnlyList<BrowserPluginActionParameterDescriptor> Parameters);

/// <summary>
/// 描述浏览器插件及其支持的控制与函数能力。
/// </summary>
/// <param name="Id">插件标识。</param>
/// <param name="Name">插件名称。</param>
/// <param name="Description">插件说明。</param>
/// <param name="State">插件状态。</param>
/// <param name="SupportsPause">是否支持暂停。</param>
/// <param name="Controls">插件控制命令集合。</param>
/// <param name="Functions">插件函数集合。</param>
public sealed record BrowserPluginDescriptor(
    string Id,
    string Name,
    string? Description,
    string State,
    bool SupportsPause,
    IReadOnlyList<BrowserPluginControlDescriptor> Controls,
    IReadOnlyList<BrowserPluginFunctionDescriptor> Functions);

/// <summary>
/// 表示执行插件控制命令的请求。
/// </summary>
/// <param name="PluginId">插件标识。</param>
/// <param name="Command">控制命令标识。</param>
/// <param name="Arguments">命令参数字典。</param>
public sealed record BrowserPluginControlRequest(string PluginId, string Command, IReadOnlyDictionary<string, string?>? Arguments);

/// <summary>
/// 表示执行插件函数的请求。
/// </summary>
/// <param name="PluginId">插件标识。</param>
/// <param name="FunctionId">函数标识。</param>
/// <param name="Arguments">函数参数字典。</param>
public sealed record BrowserPluginFunctionExecutionRequest(string PluginId, string FunctionId, IReadOnlyDictionary<string, string?>? Arguments);

/// <summary>
/// 表示插件控制命令或函数执行后的响应结果。
/// </summary>
/// <param name="PluginId">插件标识。</param>
/// <param name="TargetId">目标标识（命令或函数）。</param>
/// <param name="Message">响应消息。</param>
/// <param name="State">执行后的状态。</param>
/// <param name="Data">响应数据字典。</param>
public sealed record BrowserPluginExecutionResponse(string PluginId, string TargetId, string? Message, string State, IReadOnlyDictionary<string, string?> Data);

/// <summary>
/// 表示插件输出更新事件。
/// </summary>
/// <param name="PluginId">插件标识。</param>
/// <param name="TargetId">目标标识（命令或函数）。</param>
/// <param name="Message">输出消息。</param>
/// <param name="Data">输出数据字典。</param>
/// <param name="OpenModal">是否打开弹窗。</param>
/// <param name="TimestampUtc">UTC 时间戳。</param>
public sealed record BrowserPluginOutputUpdate(string PluginId, string TargetId, string? Message, IReadOnlyDictionary<string, string?> Data, bool OpenModal, DateTimeOffset TimestampUtc);

/// <summary>
/// 表示插件目录响应结果。
/// </summary>
/// <param name="Plugins">插件描述集合。</param>
/// <param name="Errors">错误信息集合。</param>
public sealed record BrowserPluginCatalogResponse(IReadOnlyList<BrowserPluginDescriptor> Plugins, IReadOnlyList<string> Errors);

