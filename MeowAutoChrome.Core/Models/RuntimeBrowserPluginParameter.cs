namespace MeowAutoChrome.Core.Models;

public sealed class RuntimeBrowserPluginParameter
{
    public string Name { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? DefaultValue { get; init; }
    public bool Required { get; init; }
    public string InputType { get; init; } = "text";
    public IReadOnlyList<(string Value, string Label)> Options { get; init; } = Array.Empty<(string, string)>();
}
