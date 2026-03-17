using System;

namespace MeowAutoChrome.Contracts.Attributes;

/// <summary>
/// 浏览器插件操作特性，用于标记浏览器插件的操作方法。
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class PActionAttribute : Attribute
{
    /// <summary>
    /// 标记浏览器插件操作的方法的唯一标识符。
    /// </summary>
    public string? Id { get; }
    /// <summary>
    /// 标记浏览器插件操作的方法的名称。
    /// </summary>
    public string Name { get; }
    /// <summary>
    /// 方法描述，标记浏览器插件操作的方法的描述信息。
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 浏览器插件操作特性，用于标记导出插件的操作方法。
    /// </summary>
    /// <param name="name">标记浏览器插件操作的方法的名称。</param>
    public PActionAttribute(string name)
    {
        Name = name;
    }
    /// <summary>
    /// 浏览器插件操作特性，用于标记导出插件的操作方法。
    /// </summary>
    /// <param name="id">标记浏览器插件操作的方法的唯一标识符。</param>
    /// <param name="name">标记浏览器插件操作的方法的名称。</param>
    public PActionAttribute(string id, string name)
    {
        Id = id;
        Name = name;
    }
}


