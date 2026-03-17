namespace MeowAutoChrome.Contracts.BrowserPlugin;
/// <summary>
/// 插件状态枚举，表示插件当前的运行状态，包括停止、运行和暂停三种状态，插件可以根据自己的逻辑切换状态，并通过结果数据向宿主或前端展示当前状态信息
/// </summary>
public enum PluginState
{
    /// <summary>
    /// 停止状态，表示插件当前未运行，可能是初始状态或已停止状态，插件在这个状态下通常不执行任何操作，等待被启动
    /// </summary>
    Stopped,
    /// <summary>
    /// 运行状态，表示插件当前正在运行，执行其核心功能，插件在这个状态下通常会持续执行某些操作，直到被暂停或停止
    /// </summary>
    Running,
    /// <summary>
    /// 暂停状态，表示插件当前处于暂停状态，暂时停止执行核心功能，但保持某些必要的资源或状态，插件在这个状态下通常可以恢复到运行状态，继续执行之前的操作
    /// </summary>
    Paused
}


