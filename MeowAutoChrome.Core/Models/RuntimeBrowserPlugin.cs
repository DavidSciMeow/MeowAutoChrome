using System;
using System.Collections.Generic;
using System.Reflection;

namespace MeowAutoChrome.Core.Models;

public sealed class RuntimeBrowserPlugin
{
    public string Id { get; }
    public string Name { get; }
    public string? Description { get; }
    public Type Type { get; }
    public List<RuntimeBrowserPluginControl> Controls { get; }
    public List<RuntimeBrowserPluginAction> Actions { get; }

    public RuntimeBrowserPlugin(string id, string name, string? description, Type type, List<RuntimeBrowserPluginControl> controls, List<RuntimeBrowserPluginAction> actions)
    {
        Id = id;
        Name = name;
        Description = description;
        Type = type;
        Controls = controls;
        Actions = actions;
    }
}

public sealed class RuntimeBrowserPluginAction
{
    public string Id { get; }
    public string Name { get; }
    public string? Description { get; }
    public MethodInfo Method { get; }
    public RuntimeBrowserPluginParameter[] Parameters { get; }

    public RuntimeBrowserPluginAction(string id, string name, string? description, MethodInfo method, RuntimeBrowserPluginParameter[] parameters)
    {
        Id = id;
        Name = name;
        Description = description;
        Method = method;
        Parameters = parameters;
    }
}

public sealed class RuntimeBrowserPluginControl
{
    public string Command { get; }
    public string Name { get; }
    public string? Description { get; }
    public RuntimeBrowserPluginParameter[] Parameters { get; }

    public RuntimeBrowserPluginControl(string command, string name, string? description, RuntimeBrowserPluginParameter[] parameters)
    {
        Command = command;
        Name = name;
        Description = description;
        Parameters = parameters;
    }
}

public sealed class RuntimeBrowserPluginParameter
{
    public string Name { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? DefaultValue { get; init; }
    public bool Required { get; init; }
    public string InputType { get; init; } = "text";
    public IReadOnlyList<(string Value, string Label)> Options { get; init; } = Array.Empty<(string, string)>();
}
