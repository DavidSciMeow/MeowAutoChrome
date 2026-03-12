using System;

namespace MeowAutoChrome.Contracts;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class BrowserPluginAttribute(string id, string name) : Attribute
{
    public string Id { get; } = id;
    public string Name { get; } = name;
    public string? Description { get; init; }
    public string ApiVersion { get; init; } = BrowserPluginApi.CurrentVersion;
}


