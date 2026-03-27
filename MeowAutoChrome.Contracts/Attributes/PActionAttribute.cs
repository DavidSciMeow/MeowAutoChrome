using System;

namespace MeowAutoChrome.Contracts.Attributes;

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class PActionAttribute : Attribute
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
}
