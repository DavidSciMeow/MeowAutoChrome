using System;

namespace MeowAutoChrome.Contracts.Attributes;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class PluginAttribute : Attribute
{
    public string Id { get; }
    public string Name { get; }
    public string? Description { get; set; }

    public PluginAttribute(string id, string name)
    {
        Id = id;
        Name = name;
    }
}
