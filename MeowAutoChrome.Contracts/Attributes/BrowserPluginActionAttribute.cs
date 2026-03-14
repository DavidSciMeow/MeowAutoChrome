using System;

namespace MeowAutoChrome.Contracts.Attributes;

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class BrowserPluginActionAttribute : Attribute
{
    public BrowserPluginActionAttribute(string name)
    {
        Name = name;
    }

    public BrowserPluginActionAttribute(string id, string name)
    {
        Id = id;
        Name = name;
    }

    public string? Id { get; }
    public string Name { get; }
    public string? Description { get; init; }
}


