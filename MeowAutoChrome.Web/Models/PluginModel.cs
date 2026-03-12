namespace MeowAutoChrome.Web.Models;

public sealed record BrowserPluginActionParameterDescriptor(string Name, string Label, string? Description, string? DefaultValue, bool Required);

public sealed record BrowserPluginFunctionDescriptor(string Id, string Name, string? Description, IReadOnlyList<BrowserPluginActionParameterDescriptor> Parameters);

public sealed record BrowserPluginDescriptor(
    string Id,
    string Name,
    string? Description,
    string State,
    bool SupportsPause,
    IReadOnlyList<BrowserPluginFunctionDescriptor> Functions);

public sealed record BrowserPluginControlRequest(string PluginId, string Command, IReadOnlyDictionary<string, string?>? Arguments);

public sealed record BrowserPluginFunctionExecutionRequest(string PluginId, string FunctionId, IReadOnlyDictionary<string, string?>? Arguments);

public sealed record BrowserPluginExecutionResponse(string PluginId, string TargetId, string? Message, string State, IReadOnlyDictionary<string, string?> Data);

