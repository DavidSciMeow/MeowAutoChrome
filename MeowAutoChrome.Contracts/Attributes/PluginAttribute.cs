using MeowAutoChrome.Contracts.BrowserPlugin;
using System;

namespace MeowAutoChrome.Contracts.Attributes;

/// <summary>
/// 浏览器插件特性，用于标记浏览器插件的类。
/// </summary>
/// <param name="id">导出插件的唯一标识符</param>
/// <param name="name">导出的插件名称</param>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class PluginAttribute(string id, string name) : Attribute
{
    /// <summary>
    /// 导出插件的唯一标识符，必须在同一插件中唯一。
    /// </summary>
    public string Id { get; } = id;
    /// <summary>
    /// 导出插件的名称
    /// </summary>
    public string Name { get; } = name;
    /// <summary>
    /// 导出插件的描述信息
    /// </summary>
    public string? Description { get; init; }
    /// <summary>
    /// 导出插件版本，默认为当前版本号，必须符合语义化版本规范。
    /// </summary>
    public string ApiVersion { get; init; } = PluginApi.CurrentVersion;
}


