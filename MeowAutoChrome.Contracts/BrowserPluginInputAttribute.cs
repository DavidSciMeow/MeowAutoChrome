using System;

namespace MeowAutoChrome.Contracts;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class BrowserPluginInputAttribute(string name, string label) : Attribute
{
    public string Name { get; } = name;
    public string Label { get; } = label;
    public string? Description { get; init; }
    public string? DefaultValue { get; init; }
    public bool Required { get; init; }
}


