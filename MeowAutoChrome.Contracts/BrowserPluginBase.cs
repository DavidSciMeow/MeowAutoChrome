using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace MeowAutoChrome.Contracts;

public abstract class BrowserPluginBase : IBrowserPlugin
{
    public BrowserPluginState State { get; protected set; } = BrowserPluginState.Stopped;
    public IHostContext? HostContext { get; set; }

    public virtual bool SupportsPause => false;

    protected IBrowserContext CurrentBrowserContext => HostContext?.BrowserContext ?? throw new InvalidOperationException("宿主尚未注入 BrowserContext。");
    protected IPage? CurrentActivePage => HostContext?.ActivePage;
    protected string CurrentBrowserInstanceId => HostContext?.BrowserInstanceId ?? throw new InvalidOperationException("宿主尚未提供 BrowserInstanceId。");
    protected IBrowserInstanceManager CurrentBrowserInstanceManager => HostContext?.BrowserInstanceManager ?? throw new InvalidOperationException("宿主尚未提供 BrowserInstanceManager。");
    protected IReadOnlyDictionary<string, string?> CurrentArguments => HostContext?.Arguments ?? EmptyArguments;
    protected string CurrentPluginId => HostContext?.PluginId ?? throw new InvalidOperationException("宿主尚未提供 PluginId。");
    protected string CurrentTargetId => HostContext?.TargetId ?? throw new InvalidOperationException("宿主尚未提供 TargetId。");
    protected CancellationToken CurrentCancellationToken => HostContext?.CancellationToken ?? CancellationToken.None;

    protected virtual string PluginName => "插件";
    protected virtual string AlreadyRunningMessage => $"{PluginName}已处于运行中。";
    protected virtual string StartedMessage => $"{PluginName}已启动。";
    protected virtual string StoppedMessage => $"{PluginName}已停止。";
    protected virtual string PauseNotSupportedMessage => $"{PluginName}不支持暂停。";
    protected virtual string PauseRequiresRunningMessage => "只有运行中的插件才能暂停。";
    protected virtual string PausedMessage => $"{PluginName}已暂停。";
    protected virtual string ResumeRequiresPausedMessage => "只有暂停中的插件才能恢复。";
    protected virtual string ResumedMessage => $"{PluginName}已恢复。";

    private static readonly IReadOnlyDictionary<string, string?> EmptyArguments = new Dictionary<string, string?>();

    protected IPage RequireActivePage() => CurrentActivePage ?? throw new InvalidOperationException("宿主尚未提供活动页面。");

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

    public virtual Task<BrowserPluginActionResult> StartAsync()
    {
        CurrentCancellationToken.ThrowIfCancellationRequested();

        if (State == BrowserPluginState.Running)
            return this.Ok(AlreadyRunningMessage);

        State = BrowserPluginState.Running;
        return this.Ok(StartedMessage);
    }

    public virtual Task<BrowserPluginActionResult> StopAsync()
    {
        CurrentCancellationToken.ThrowIfCancellationRequested();
        State = BrowserPluginState.Stopped;
        return this.Ok(StoppedMessage);
    }

    public virtual Task<BrowserPluginActionResult> PauseAsync()
    {
        CurrentCancellationToken.ThrowIfCancellationRequested();

        if (!SupportsPause)
            return this.Ok(PauseNotSupportedMessage);

        if (State != BrowserPluginState.Running)
            return this.Ok(PauseRequiresRunningMessage);

        State = BrowserPluginState.Paused;
        return this.Ok(PausedMessage);
    }

    public virtual Task<BrowserPluginActionResult> ResumeAsync()
    {
        CurrentCancellationToken.ThrowIfCancellationRequested();

        if (!SupportsPause)
            return this.Ok(PauseNotSupportedMessage);

        if (State != BrowserPluginState.Paused)
            return this.Ok(ResumeRequiresPausedMessage);

        State = BrowserPluginState.Running;
        return this.Ok(ResumedMessage);
    }
}
