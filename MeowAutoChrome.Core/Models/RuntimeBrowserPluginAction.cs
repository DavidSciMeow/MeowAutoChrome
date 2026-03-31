using System.Reflection;

namespace MeowAutoChrome.Core.Models;

/// <summary>
/// 运行时插件动作的元数据，包含方法信息与参数描述。<br/>
/// Metadata for a runtime plugin action, including method info and parameters.
/// </summary>
public sealed class RuntimeBrowserPluginAction
{
    /// <summary>
    /// 动作 ID。<br/>
    /// Action id.
    /// </summary>
    public string Id { get; }
    /// <summary>
    /// 动作名称。<br/>
    /// Action name.
    /// </summary>
    public string Name { get; }
    /// <summary>
    /// 动作描述（可空）。<br/>
    /// Action description (nullable).
    /// </summary>
    public string? Description { get; }
    /// <summary>
    /// 反射方法信息。<br/>
    /// Reflection MethodInfo for the action.
    /// </summary>
    public MethodInfo Method { get; }
    /// <summary>
    /// 参数描述数组。<br/>
    /// Array of parameter descriptors.
    /// </summary>
    public RuntimeBrowserPluginParameter[] Parameters { get; }

    /// <summary>
    /// 构造运行时插件动作元数据实例。<br/>
    /// Construct a runtime plugin action metadata instance.
    /// </summary>
    /// <param name="id">动作 ID / action id.</param>
    /// <param name="name">动作名称 / action name.</param>
    /// <param name="description">动作描述（可空）/ action description (nullable).</param>
    /// <param name="method">关联的方法信息 / associated MethodInfo.</param>
    /// <param name="parameters">参数描述数组 / array of parameter descriptors.</param>
    public RuntimeBrowserPluginAction(string id, string name, string? description, MethodInfo method, RuntimeBrowserPluginParameter[] parameters)
    {
        Id = id;
        Name = name;
        Description = description;
        Method = method;
        Parameters = parameters;
    }
}
