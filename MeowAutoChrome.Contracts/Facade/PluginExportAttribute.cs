using System;

namespace MeowAutoChrome.Contracts.Facade;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false)]
public sealed class PluginExportAttribute : Attribute
{
    public string Id { get; }
    public string Name { get; }

    public PluginExportAttribute(string id, string name)
    {
        Id = id;
        Name = name;
    }
}
