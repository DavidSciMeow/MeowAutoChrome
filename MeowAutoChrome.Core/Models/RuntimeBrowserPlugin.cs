using System;
using System.Collections.Generic;

namespace MeowAutoChrome.Core.Models;

public sealed class RuntimeBrowserPlugin(string id, string name, string? description, Type type, List<RuntimeBrowserPluginControl> controls, List<RuntimeBrowserPluginAction> actions)
{
    public string Id { get; } = id;
    public string Name { get; } = name;
    public string? Description { get; } = description;
    public Type Type { get; } = type;
    public List<RuntimeBrowserPluginControl> Controls { get; } = controls;
    public List<RuntimeBrowserPluginAction> Actions { get; } = actions;
}
