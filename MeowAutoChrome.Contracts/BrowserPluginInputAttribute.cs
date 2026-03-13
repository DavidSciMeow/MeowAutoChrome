using System;

namespace MeowAutoChrome.Contracts;

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
}


