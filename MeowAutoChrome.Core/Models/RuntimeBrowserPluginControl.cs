namespace MeowAutoChrome.Core.Models;

public sealed class RuntimeBrowserPluginControl
{
    public string Command { get; }
    public string Name { get; }
    public string? Description { get; }
    public RuntimeBrowserPluginParameter[] Parameters { get; }

    public RuntimeBrowserPluginControl(string command, string name, string? description, RuntimeBrowserPluginParameter[] parameters)
    {
        Command = command;
        Name = name;
        Description = description;
        Parameters = parameters;
    }
}
