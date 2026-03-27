using System;

namespace MeowAutoChrome.Contracts.Attributes;

[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property, AllowMultiple = true)]
public sealed class PInputAttribute : Attribute
{
    public string? Name { get; set; }
    public string? Label { get; set; }
    public string? Description { get; set; }
    public string? DefaultValue { get; set; }
    public bool Required { get; set; }
    public string? InputType { get; set; }
}
