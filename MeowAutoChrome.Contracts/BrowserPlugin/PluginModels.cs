using System;
using System.Collections.Generic;

namespace MeowAutoChrome.Contracts.BrowserPlugin;

/// <summary>
/// Represents an option for a plugin action parameter.
/// </summary>
public sealed record BrowserPluginActionParameterOptionDescriptor(string Value, string Label);

/// <summary>
/// Describes a plugin action parameter.
/// </summary>
public sealed record BrowserPluginActionParameterDescriptor(
    string Name,
    string Label,
    string? Description,
    string? DefaultValue,
    bool Required,
    string InputType,
    IReadOnlyList<BrowserPluginActionParameterOptionDescriptor> Options);

/// <summary>
/// Describes a control command for a plugin.
/// </summary>
public sealed record BrowserPluginControlDescriptor(
    string Command,
    string Name,
    string? Description,
    IReadOnlyList<BrowserPluginActionParameterDescriptor> Parameters);

/// <summary>
/// Describes a plugin function and its parameters.
/// </summary>
public sealed record BrowserPluginFunctionDescriptor(string Id, string Name, string? Description, IReadOnlyList<BrowserPluginActionParameterDescriptor> Parameters);

/// <summary>
/// Describes a browser plugin and its capabilities.
/// </summary>
public sealed record BrowserPluginDescriptor(
    string Id,
    string Name,
    string? Description,
    string State,
    bool SupportsPause,
    IReadOnlyList<BrowserPluginControlDescriptor> Controls,
    IReadOnlyList<BrowserPluginFunctionDescriptor> Functions);

/// <summary>
/// Represents a request to control a plugin.
/// </summary>
public sealed record BrowserPluginControlRequest(string PluginId, string Command, IReadOnlyDictionary<string, string?>? Arguments);

/// <summary>
/// Represents a request to execute a plugin function.
/// </summary>
public sealed record BrowserPluginFunctionExecutionRequest(string PluginId, string FunctionId, IReadOnlyDictionary<string, string?>? Arguments);

/// <summary>
/// Represents the response after executing a plugin control or function.
/// </summary>
public sealed record BrowserPluginExecutionResponse(string PluginId, string TargetId, string? Message, string State, IReadOnlyDictionary<string, string?> Data);

/// <summary>
/// Represents a plugin output update event.
/// </summary>
public sealed record BrowserPluginOutputUpdate(string PluginId, string TargetId, string? Message, IReadOnlyDictionary<string, string?> Data, bool OpenModal, DateTimeOffset TimestampUtc);

/// <summary>
/// Detailed error information for plugin discovery failures.
/// </summary>
public sealed record BrowserPluginErrorDescriptor(string? Assembly, string Summary, string Detail);

/// <summary>
/// Plugin catalog response containing plugins and optional errors.
/// </summary>
public sealed record BrowserPluginCatalogResponse(IReadOnlyList<BrowserPluginDescriptor?> Plugins, IReadOnlyList<string> Errors, IReadOnlyList<BrowserPluginErrorDescriptor> ErrorsDetailed);
