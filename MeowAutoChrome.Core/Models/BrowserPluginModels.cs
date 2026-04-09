namespace MeowAutoChrome.Core.Models;

/// <summary>
/// 参数选项描述，如用于下拉或枚举选项。<br/>
/// Descriptor for parameter options (e.g., dropdown entries).
/// </summary>
/// <param name="Value">选项值 / option value.</param>
/// <param name="Label">显示标签 / display label.</param>
public sealed record BrowserPluginActionParameterOptionDescriptor(string Value, string Label);

/// <summary>
/// 动作参数描述符（名称、标签、默认值等）。<br/>
/// Descriptor for an action parameter (name, label, default value, etc.).
/// </summary>
/// <param name="Name">参数名称（键）/ parameter name (key).</param>
/// <param name="Label">显示标签 / display label.</param>
/// <param name="Description">参数描述（可空）/ parameter description (nullable).</param>
/// <param name="DefaultValue">默认值（可空）/ default value (nullable).</param>
/// <param name="Required">是否必填 / whether required.</param>
/// <param name="InputType">输入类型提示 / input type hint.</param>
/// <param name="Options">可选项列表 / list of options.</param>
public sealed record BrowserPluginActionParameterDescriptor(
    string Name,
    string Label,
    string? Description,
    string? DefaultValue,
    bool Required,
    string InputType,
    IReadOnlyList<BrowserPluginActionParameterOptionDescriptor> Options);

/// <summary>
/// 控件（命令）描述符，包含命令 ID、名称与参数列表。<br/>
/// Descriptor for a control/command including command id, name and parameters.
/// </summary>
/// <param name="Command">控制命令标识 / control command id.</param>
/// <param name="Name">控制显示名称 / control display name.</param>
/// <param name="Description">控制描述（可空）/ control description (nullable).</param>
/// <param name="Parameters">参数描述列表 / list of parameter descriptors.</param>
public sealed record BrowserPluginControlDescriptor(
    string Command,
    string Name,
    string? Description,
    IReadOnlyList<BrowserPluginActionParameterDescriptor> Parameters);

/// <summary>
/// 插件函数/方法描述符，包含函数 ID、名称与参数。<br/>
/// Descriptor for a plugin function/method with id, name and parameters.
/// </summary>
/// <param name="Id">函数/动作 ID / function/action id.</param>
/// <param name="Name">函数名称 / function name.</param>
/// <param name="Description">函数描述（可空）/ function description (nullable).</param>
/// <param name="Parameters">参数描述列表 / list of parameter descriptors.</param>
public sealed record BrowserPluginFunctionDescriptor(string Id, string Name, string? Description, IReadOnlyList<BrowserPluginActionParameterDescriptor> Parameters);

/// <summary>
/// 插件描述符，包含元数据和可用命令/函数。<br/>
/// Plugin descriptor including metadata and available controls/functions.
/// </summary>
/// <param name="Id">插件 ID / plugin id.</param>
/// <param name="Name">插件名称 / plugin name.</param>
/// <param name="Description">插件描述（可空）/ plugin description (nullable).</param>
/// <param name="State">插件当前状态 / current plugin state.</param>
/// <param name="SupportsPause">是否支持暂停 / whether supports pause.</param>
/// <param name="Controls">控制/命令描述列表 / list of control descriptors.</param>
/// <param name="Functions">函数/动作描述列表 / list of function descriptors.</param>
public sealed record BrowserPluginDescriptor(
    string Id,
    string Name,
    string? Description,
    string State,
    bool SupportsPause,
    IReadOnlyList<BrowserPluginControlDescriptor> Controls,
    IReadOnlyList<BrowserPluginFunctionDescriptor> Functions);

/// <summary>
/// 请求执行插件控制命令的载荷。<br/>
/// Payload to request plugin control command execution.
/// </summary>
/// <param name="PluginId">目标插件 ID / target plugin id.</param>
/// <param name="Command">控制命令 / control command.</param>
/// <param name="Arguments">可选参数字典 / optional arguments dictionary.</param>
public sealed record BrowserPluginControlRequest(string PluginId, string Command, IReadOnlyDictionary<string, string?>? Arguments);

/// <summary>
/// 请求执行插件函数的载荷。<br/>
/// Payload to request plugin function execution.
/// </summary>
/// <param name="PluginId">目标插件 ID / target plugin id.</param>
/// <param name="FunctionId">要执行的函数 ID / function id to execute.</param>
/// <param name="Arguments">可选参数字典 / optional arguments dictionary.</param>
public sealed record BrowserPluginFunctionExecutionRequest(string PluginId, string FunctionId, IReadOnlyDictionary<string, string?>? Arguments);

/// <summary>
/// 插件执行响应，包含目标 ID、消息与数据。<br/>
/// Plugin execution response containing target id, message and data.
/// </summary>
/// <param name="PluginId">源插件 ID / source plugin id.</param>
/// <param name="TargetId">目标 ID（例如控制命令/函数 id）/ target id (e.g., control/command id).</param>
/// <param name="Message">可选消息文本 / optional message text.</param>
/// <param name="State">目标状态字符串 / state string.</param>
/// <param name="Data">可选返回数据 / optional returned data.</param>
public sealed record BrowserPluginExecutionResponse(string PluginId, string TargetId, string? Message, string State, object? Data);

/// <summary>
/// 插件输出更新事件的描述（例如用于 UI 推送）。<br/>
/// Descriptor for a plugin output update event (e.g., for UI push).
/// </summary>
/// <param name="PluginId">插件 ID / plugin id.</param>
/// <param name="TargetId">目标 ID / target id.</param>
/// <param name="Message">消息文本 / message text.</param>
/// <param name="Data">附带的键值数据 / attached key/value data.</param>
/// <param name="ToastRequested">是否请求额外显示 toast / whether an additional toast should be requested.</param>
/// <param name="TimestampUtc">事件时间戳（UTC）/ event timestamp (UTC).</param>
public sealed record BrowserPluginOutputUpdate(string PluginId, string TargetId, string? Message, IReadOnlyDictionary<string, string?> Data, bool ToastRequested, DateTimeOffset TimestampUtc);

/// <summary>
/// 插件错误描述符，包含程序集、摘要与详细信息。<br/>
/// Descriptor of plugin errors including assembly, summary and detail.
/// </summary>
/// <param name="Assembly">出错的程序集名（可空）/ assembly name (nullable).</param>
/// <param name="Summary">错误摘要 / brief error summary.</param>
/// <param name="Detail">错误详细信息 / detailed error information.</param>
public sealed record BrowserPluginErrorDescriptor(string? Assembly, string Summary, string Detail);

/// <summary>
/// 插件目录响应，包含发现的插件列表与错误信息。<br/>
/// Plugin catalog response including discovered plugins and errors.
/// </summary>
/// <param name="Plugins">发现的插件描述符列表 / discovered plugin descriptors.</param>
/// <param name="Errors">简要错误信息列表 / brief error messages.</param>
/// <param name="ErrorsDetailed">详细错误描述列表 / detailed error descriptors.</param>
public sealed record BrowserPluginCatalogResponse(IReadOnlyList<BrowserPluginDescriptor?> Plugins, IReadOnlyList<string> Errors, IReadOnlyList<BrowserPluginErrorDescriptor> ErrorsDetailed);
