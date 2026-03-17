using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MeowAutoChrome.Contracts.Interface;
using Microsoft.Playwright;

namespace MeowAutoChrome.Contracts.BrowserPlugin;

/// <summary>
/// 插件抽象基类，提供插件的基本状态管理和与宿主交互的便利方法，插件开发者可以继承这个基类来实现自己的插件逻辑，而不需要重复实现状态管理和与宿主交互的代码，从而专注于插件的核心功能开发
/// </summary>
public abstract class PluginBase : IPlugin
{
    /// <summary>
    /// 插件的当前状态，初始为停止状态，插件可以通过这个属性让宿主了解自己的状态，以便进行相应的管理和展示，插件在启动、停止、暂停和恢复等操作中会更新这个状态，以反映当前的运行情况
    /// </summary>
    public PluginState State { get; protected set; } = PluginState.Stopped;
    /// <summary>
    /// 宿主上下文，表示当前插件所在的宿主环境的上下文信息，包括浏览器上下文、活动页面、浏览器实例ID、浏览器实例管理器、插件启动参数、插件ID、目标ID和取消令牌等信息，插件可以通过这个属性获取宿主提供的相关信息和功能，以便在自己的操作中使用和交互，宿主会在插件启动时注入这个属性，并且在插件停止时清除这个属性，以便插件正确地管理自己的生命周期和资源
    /// </summary>
    public IHostContext? HostContext { get; set; }
    /// <summary>
    /// 是否支持暂停，表示当前插件是否支持暂停和恢复的功能，如果返回true，则宿主会提供相应的操作界面和功能来允许用户暂停和恢复插件的运行，如果返回false，则宿主会隐藏相关的操作界面，并且在用户尝试暂停或恢复插件时返回一个错误结果，插件可以通过这个属性告知宿主自己的能力，以便宿主进行相应的管理和展示，默认实现返回false，表示不支持暂停，插件开发者可以重写这个属性来支持暂停功能
    /// </summary>
    public virtual bool SupportsPause => false;

    /// <summary>
    /// 当前浏览器上下文，插件可以通过这个属性获取当前浏览器实例的上下文，以便在该实例中执行浏览器相关的操作或获取相关的信息，如果宿主尚未注入BrowserContext，则抛出一个InvalidOperationException异常，提示宿主尚未注入BrowserContext，插件开发者可以在需要操作浏览器上下文的逻辑中调用这个属性，以确保能够正确地获取到浏览器上下文并进行相应的操作
    /// </summary>
    protected IBrowserContext CurrentBrowserContext => HostContext?.BrowserContext ?? throw new InvalidOperationException("宿主尚未注入 BrowserContext。");
    /// <summary>
    /// 当前活动页面，插件可以通过这个属性获取当前浏览器实例的活动页面，以便在该页面中执行浏览器相关的操作或获取相关的信息，如果没有活动页面，则返回null，插件开发者可以在需要操作页面的逻辑中调用这个属性，以获取当前活动页面的信息或进行相应的操作，如果宿主尚未提供活动页面，则返回null，插件开发者需要根据实际情况进行处理，例如提示用户没有活动页面或者等待活动页面的提供等
    /// </summary>
    protected IPage? CurrentActivePage => HostContext?.ActivePage;
    /// <summary>
    /// 当前浏览器实例ID，插件可以通过这个属性获取当前浏览器实例的ID，以便在与宿主进行交互时使用该ID进行区分或识别，如果宿主尚未提供浏览器实例ID，则抛出一个InvalidOperationException异常，提示宿主尚未提供BrowserInstanceId，插件开发者可以在需要使用浏览器实例ID的逻辑中调用这个属性，以确保能够正确地获取到浏览器实例ID并进行相应的操作
    /// </summary>
    protected string CurrentBrowserInstanceId => HostContext?.BrowserInstanceId ?? throw new InvalidOperationException("宿主尚未提供 BrowserInstanceId。");
    /// <summary>
    /// 当前浏览器实例管理器，插件可以通过这个属性获取浏览器实例管理器，以便在需要获取其他浏览器实例的信息或执行跨实例的操作时使用该管理器进行相关的操作，如果宿主尚未提供浏览器实例管理器，则抛出一个InvalidOperationException异常，提示宿主尚未提供BrowserInstanceManager，插件开发者可以在需要使用浏览器实例管理器的逻辑中调用这个属性，以确保能够正确地获取到浏览器实例管理器并进行相应的操作
    /// </summary>
    protected IBrowserInstanceManager CurrentBrowserInstanceManager => HostContext?.BrowserInstanceManager ?? throw new InvalidOperationException("宿主尚未提供 BrowserInstanceManager。");
    /// <summary>
    /// 当前插件启动参数，包含插件启动时传递的键值对参数，插件可以通过这个属性获取插件启动时传递的参数，以便在运行时使用这些参数进行相关的操作或配置，如果没有参数则为一个空字典，插件开发者可以在需要使用启动参数的逻辑中调用这个属性，以确保能够正确地获取到启动参数并进行相应的操作，如果宿主尚未提供参数，则返回一个空字典，插件开发者需要根据实际情况进行处理，例如使用默认参数或者提示用户提供必要的参数等
    /// </summary>
    protected IReadOnlyDictionary<string, string?> CurrentArguments => HostContext?.Arguments ?? EmptyArguments;
    /// <summary>
    /// 当前插件ID，插件可以通过这个属性获取当前插件的ID，以便在与宿主进行交互时使用该ID进行区分或识别，如果宿主尚未提供插件ID，则抛出一个InvalidOperationException异常，提示宿主尚未提供PluginId，插件开发者可以在需要使用插件ID的逻辑中调用这个属性，以确保能够正确地获取到插件ID并进行相应的操作
    /// </summary>
    protected string CurrentPluginId => HostContext?.PluginId ?? throw new InvalidOperationException("宿主尚未提供 PluginId。");
    /// <summary>
    /// 当前目标ID，表示当前插件的目标对象的ID，插件可以通过这个属性获取当前插件的目标ID，以便在与宿主进行交互时使用该ID进行区分或识别，如果宿主尚未提供目标ID，则抛出一个InvalidOperationException异常，提示宿主尚未提供TargetId，插件开发者可以在需要使用目标ID的逻辑中调用这个属性，以确保能够正确地获取到目标ID并进行相应的操作，如果没有特定的目标对象，插件可以根据实际需求进行相应的处理，例如使用默认目标ID或者提示用户提供必要的目标ID等
    /// </summary>
    protected string CurrentTargetId => HostContext?.TargetId ?? throw new InvalidOperationException("宿主尚未提供 TargetId。");
    /// <summary>
    /// 插件的取消令牌，插件可以通过这个属性获取取消令牌，以便在需要支持取消操作的场景中使用该令牌进行相关的操作，如取消正在执行的任务或操作，如果宿主尚未提供取消令牌，则返回一个默认的不可取消的令牌，插件开发者可以在需要支持取消操作的逻辑中调用这个属性，以确保能够正确地获取到取消令牌并进行相应的操作，如果没有提供取消令牌，则表示不支持取消操作，插件开发者需要根据实际情况进行处理，例如不执行取消相关的逻辑或者提示用户不支持取消操作等
    /// </summary>
    protected CancellationToken CurrentCancellationToken => HostContext?.CancellationToken ?? CancellationToken.None;

    /// <summary>
    /// 获取插件名称，默认实现返回一个通用的字符串"插件"，插件开发者可以重写这个属性来提供具体的插件名称，以便在各种消息中使用更具体或更友好的名称来指代插件，从而提升用户体验和信息的清晰度
    /// </summary>
    protected virtual string PluginName => "插件";
    /// <summary>
    /// 获取插件已处于运行中的消息，默认实现返回一个格式化的字符串，表示插件已处于运行中，插件开发者可以重写这个属性来提供自定义的消息，以便在用户尝试启动一个已经在运行中的插件时向用户展示更具体或更友好的信息
    /// </summary>
    protected virtual string AlreadyRunningMessage => $"{PluginName}已处于运行中。";
    /// <summary>
    /// 获取插件已启动的消息，默认实现返回一个格式化的字符串，表示插件已启动，插件开发者可以重写这个属性来提供自定义的消息，以便在插件进入运行状态时向用户展示更具体或更友好的信息
    /// </summary>
    protected virtual string StartedMessage => $"{PluginName}已启动。";
    /// <summary>
    /// 获取插件已停止的消息，默认实现返回一个格式化的字符串，表示插件已停止，插件开发者可以重写这个属性来提供自定义的消息，以便在插件进入停止状态时向用户展示更具体或更友好的信息
    /// </summary>
    protected virtual string StoppedMessage => $"{PluginName}已停止。";
    /// <summary>
    /// 获取插件不支持暂停的消息，默认实现返回一个格式化的字符串，表示插件不支持暂停，插件开发者可以重写这个属性来提供自定义的消息，以便在用户尝试暂停不支持暂停功能的插件时向用户展示更具体或更友好的信息
    /// </summary>
    protected virtual string PauseNotSupportedMessage => $"{PluginName}不支持暂停。";
    /// <summary>
    /// 获取插件暂停需要运行的消息，默认实现返回一个字符串，表示只有运行中的插件才能暂停，插件开发者可以重写这个属性来提供自定义的暂停条件消息，以便在用户尝试暂停插件时向用户展示更具体或更友好的信息
    /// </summary>
    protected virtual string PauseRequiresRunningMessage => "只有运行中的插件才能暂停。";
    /// <summary>
    /// 获取插件已暂停的消息，默认实现返回一个格式化的字符串，表示插件已暂停，插件开发者可以重写这个属性来提供自定义的暂停消息，以便在插件进入暂停状态时向用户展示更具体或更友好的信息
    /// </summary>
    protected virtual string PausedMessage => $"{PluginName}已暂停。";
    /// <summary>
    /// 获取插件恢复需要暂停的消息，默认实现返回一个字符串，表示只有暂停中的插件才能恢复，插件开发者可以重写这个属性来提供自定义的恢复条件消息，以便在用户尝试恢复插件时向用户展示更具体或更友好的信息
    /// </summary>
    protected virtual string ResumeRequiresPausedMessage => "只有暂停中的插件才能恢复。";
    /// <summary>
    /// 获取插件已恢复的消息，默认实现返回一个格式化的字符串，表示插件已恢复，插件开发者可以重写这个属性来提供自定义的恢复消息，以便在插件恢复时向用户展示更具体或更友好的信息
    /// </summary>
    protected virtual string ResumedMessage => $"{PluginName}已恢复。";

    /// <summary>
    /// 空的参数字典，表示没有参数，插件开发者可以使用这个静态只读字段来表示没有参数的情况，以避免创建多个空字典实例，从而节省资源和提高性能
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string?> EmptyArguments = new Dictionary<string, string?>();
    /// <summary>
    /// 获取当前活动页面，如果没有活动页面则抛出异常，插件可以通过这个方法获取当前浏览器实例的活动页面，以便在该页面中执行浏览器相关的操作或获取相关的信息，如果没有活动页面，则抛出一个InvalidOperationException异常，提示宿主尚未提供活动页面，插件开发者可以在需要操作页面的逻辑中调用这个方法，以确保能够正确地获取到活动页面并进行相应的操作
    /// </summary>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    protected IPage RequireActivePage() => CurrentActivePage ?? throw new InvalidOperationException("宿主尚未提供活动页面。");
    /// <summary>
    /// 发布插件更新消息
    /// </summary>
    /// <param name="message">信息</param>
    /// <param name="data">附加数据</param>
    /// <param name="openModal">是否打开模态框</param>
    /// <returns></returns>
    protected Task PublishUpdateAsync(string? message, IReadOnlyDictionary<string, string?>? data = null, bool openModal = true)
    {
        if (HostContext is null)
            return Task.CompletedTask;

        var payload = new Dictionary<string, string?>
        {
            ["state"] = State.ToString(),
        };

        if (data is not null)
        {
            foreach (var pair in data)
                payload[pair.Key] = pair.Value;
        }

        return HostContext.PublishUpdateAsync(message, payload, openModal);
    }
    /// <summary>
    /// 默认提供的启动方法，如果不重写则使用自动实现，插件开发者可以重写这个方法来实现自己的启动逻辑，如果当前状态不允许启动，则返回相应的错误结果，否则将状态更新为运行中，并返回一个成功的结果，表示插件已成功启动，插件开发者可以在这个方法中添加自己的逻辑来处理启动操作，例如初始化资源、注册事件、启动任务等，以便让插件在启动后能够正确地运行和提供功能
    /// </summary>
    /// <returns></returns>
    public virtual Task<PluginActionResult> StartAsync()
    {
        CurrentCancellationToken.ThrowIfCancellationRequested();

        if (State == PluginState.Running)
            return this.Ok(AlreadyRunningMessage);

        State = PluginState.Running;
        return this.Ok(StartedMessage);
    }
    /// <summary>
    /// 默认提供的停止方法，如果不重写则使用自动实现，插件开发者可以重写这个方法来实现自己的停止逻辑，如果当前状态不允许停止，则返回相应的错误结果，否则将状态更新为已停止，并返回一个成功的结果，表示插件已成功停止，插件开发者可以在这个方法中添加自己的逻辑来处理停止操作，例如清理资源、保存状态等，以便让插件在停止后能够正确地释放资源和保持稳定
    /// </summary>
    /// <returns></returns>
    public virtual Task<PluginActionResult> StopAsync()
    {
        CurrentCancellationToken.ThrowIfCancellationRequested();
        State = PluginState.Stopped;
        return this.Ok(StoppedMessage);
    }
    /// <summary>
    /// 默认提供的暂停方法，如果不重写则使用自动实现，插件开发者可以重写这个方法来实现自己的暂停逻辑，如果插件不支持暂停功能或者当前状态不允许暂停，则返回相应的错误结果，否则将状态更新为已暂停，并返回一个成功的结果，表示插件已成功进入暂停状态，插件开发者可以在这个方法中添加自己的逻辑来处理暂停操作，例如暂停某些任务、冻结某些状态等，以便让插件在暂停后能够保持稳定和节省资源
    /// </summary>
    /// <returns></returns>
    public virtual Task<PluginActionResult> PauseAsync()
    {
        CurrentCancellationToken.ThrowIfCancellationRequested();

        if (!SupportsPause)
            return this.Ok(PauseNotSupportedMessage);

        if (State != PluginState.Running)
            return this.Ok(PauseRequiresRunningMessage);

        State = PluginState.Paused;
        return this.Ok(PausedMessage);
    }
    /// <summary>
    /// 默认提供的恢复方法，如果不重写则使用自动实现，插件开发者可以重写这个方法来实现自己的恢复逻辑，如果插件不支持暂停功能或者当前状态不允许恢复，则返回相应的错误结果，否则将状态更新为运行中，并返回一个成功的结果，表示插件已成功恢复，插件开发者可以在这个方法中添加自己的逻辑来处理恢复操作，例如重新启动某些任务、恢复某些资源等，以便让插件在恢复后能够继续正常工作
    /// </summary>
    /// <returns></returns>
    public virtual Task<PluginActionResult> ResumeAsync()
    {
        CurrentCancellationToken.ThrowIfCancellationRequested();

        if (!SupportsPause)
            return this.Ok(PauseNotSupportedMessage);

        if (State != PluginState.Paused)
            return this.Ok(ResumeRequiresPausedMessage);

        State = PluginState.Running;
        return this.Ok(ResumedMessage);
    }
}
