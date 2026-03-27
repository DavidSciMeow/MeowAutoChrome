using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MeowAutoChrome.Contracts;
using MeowAutoChrome.Contracts.Abstractions;
using MeowAutoChrome.Contracts.Facade;
using MeowAutoChrome.Core.Extensions;
using Microsoft.Playwright;

namespace MeowAutoChrome.Core.Services.PluginHost;

/// <summary>
/// Core-hosted plugin base implementation migrated from Contracts.
/// Hosts should prefer this core implementation; Contracts.PluginBase is obsolete.
/// </summary>
public abstract class PluginBaseCore : IPlugin
{
    public PluginState State { get; protected set; } = PluginState.Stopped;

    public IPluginContext? HostContext { get; set; }

    public virtual bool SupportsPause => false;

    protected IBrowserContext CurrentBrowserContext => HostContext?.BrowserContext ?? throw new InvalidOperationException("宿主尚未注入 BrowserContext。");
    protected IPage? CurrentActivePage => HostContext?.ActivePage;
    protected IReadOnlyDictionary<string, string?> CurrentArguments => HostContext?.Arguments ?? EmptyArguments;
    protected string CurrentPluginId => HostContext?.PluginId ?? throw new InvalidOperationException("宿主尚未提供 PluginId。");
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

        // IPluginContext facade does not define PublishUpdateAsync; host may provide updates via separate publisher.
        // If HostContext also implements legacy publish, attempt dynamic invocation for backward compatibility.
        // No-op for the facade surface. Hosts should use IPluginOutputPublisher to deliver updates.
        return Task.CompletedTask;
    }

    public virtual Task<PAResult> StartAsync()
    {
        CurrentCancellationToken.ThrowIfCancellationRequested();

        if (State == PluginState.Running)
            return this.Ok(AlreadyRunningMessage);

        State = PluginState.Running;
        return this.Ok(StartedMessage);
    }

    public virtual Task<PAResult> StopAsync()
    {
        CurrentCancellationToken.ThrowIfCancellationRequested();
        State = PluginState.Stopped;
        return this.Ok(StoppedMessage);
    }

    public virtual Task<PAResult> PauseAsync()
    {
        CurrentCancellationToken.ThrowIfCancellationRequested();

        if (!SupportsPause)
            return this.Ok(PauseNotSupportedMessage);

        if (State != PluginState.Running)
            return this.Ok(PauseRequiresRunningMessage);

        State = PluginState.Paused;
        return this.Ok(PausedMessage);
    }

    public virtual Task<PAResult> ResumeAsync()
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
