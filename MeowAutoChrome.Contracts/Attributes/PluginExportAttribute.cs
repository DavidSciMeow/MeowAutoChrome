using System;

namespace MeowAutoChrome.Contracts.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false)]
public sealed class PluginExportAttribute(string id, string name) : Attribute
{
    public string Id { get; } = id;
    public string Name { get; } = name;
}
