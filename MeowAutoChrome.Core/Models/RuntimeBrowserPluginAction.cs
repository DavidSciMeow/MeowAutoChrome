using System.Reflection;

namespace MeowAutoChrome.Core.Models;

public sealed class RuntimeBrowserPluginAction
{
    public string Id { get; }
    public string Name { get; }
    public string? Description { get; }
    public MethodInfo Method { get; }
    public RuntimeBrowserPluginParameter[] Parameters { get; }

    public RuntimeBrowserPluginAction(string id, string name, string? description, MethodInfo method, RuntimeBrowserPluginParameter[] parameters)
    {
        Id = id;
        Name = name;
        Description = description;
        Method = method;
        Parameters = parameters;
    }
}
