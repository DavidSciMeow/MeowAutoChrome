using System;

namespace MeowAutoChrome.Contracts;

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class BrowserPluginActionAttribute(string id, string name) : Attribute
{
    public string Id { get; } = id;
    public string Name { get; } = name;
    public string? Description { get; init; }
}


