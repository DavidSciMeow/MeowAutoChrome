using System;

namespace MeowAutoChrome.Contracts.Attributes;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Parameter, AllowMultiple = true, Inherited = false)]
public sealed class BrowserPluginInputAttribute : Attribute
{
    public BrowserPluginInputAttribute(string label)
    {
        Label = label;
    }

    public BrowserPluginInputAttribute(string label, string description)
    {
        Label = label;
        Description = description;
    }

    public string Label { get; }
    public string? Description { get; init; }
    public string? Name { get; set; }
    public string? DefaultValue { get; set; }
    public bool Required { get; set; }
    public string InputType { get; set; } = "text";
}


