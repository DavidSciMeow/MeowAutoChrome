using System.Collections.Generic;

namespace MeowAutoChrome.Contracts.BrowserPlugin;

/// <summary>
/// 插件操作结果
/// </summary>
/// <param name="Message">操作结果消息</param>
/// <param name="Data">操作结果数据</param>
public sealed record PluginActionResult(string? Message, IReadOnlyDictionary<string, string?>? Data = null);


