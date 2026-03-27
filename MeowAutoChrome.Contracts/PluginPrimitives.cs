using System.Collections.Generic;

namespace MeowAutoChrome.Contracts;

/// <summary>
/// 插件状态枚举，表示插件当前的运行状态，包括停止、运行和暂停三种状态
/// </summary>
public enum PluginState
{
    Stopped,
    Running,
    Paused
}

/// <summary>
/// 插件操作结果
/// </summary>
/// <param name="Message">操作结果消息</param>
/// <param name="Data">操作结果数据</param>
public sealed record PAResult(string? Message, IReadOnlyDictionary<string, string?>? Data = null);
