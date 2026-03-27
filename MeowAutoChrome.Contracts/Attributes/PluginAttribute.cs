using System;

namespace MeowAutoChrome.Contracts.Attributes;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class PluginAttribute(string id, string name, string? description = null) : Attribute
{
    public string Id { get; } = id;
    public string Name { get; } = name;
    public string? Description { get; set; } = description;
}
