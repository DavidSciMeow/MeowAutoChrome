using System;
using System.Threading.Tasks;
using MeowAutoChrome.Contracts;
using MeowAutoChrome.Contracts.Abstractions;
using MeowAutoChrome.Contracts.Attributes;

namespace MeowAutoChrome.ExamplePlugin;

[Plugin("example.minimal", "Example Minimal Plugin", Description = "示例：基于最小 Contracts 表面实现的插件。")]
public sealed class MinimalExamplePlugin : IPlugin, IAsyncDisposable
{
    public PluginState State { get; private set; } = PluginState.Stopped;

    public bool SupportsPause => false;

    [Obsolete("Legacy host context. Host will inject IPluginContext facade. Prefer MeowAutoChrome.Contracts.Facade.IPluginContext in future.")]
    public MeowAutoChrome.Contracts.Facade.IPluginContext? HostContext { get; set; }

    public async Task<PAResult> StartAsync()
    {
        State = PluginState.Running;

        try
        {
            if (HostContext?.ActivePage is not null)
            {
                string? title = null;
                try
                {
                    title = await HostContext.ActivePage.TitleAsync();
                }
                catch
                {
                    // swallow, best-effort
                }

                return new PAResult(title is null ? "Started. Active page present but title unavailable." : $"Active page title: {title}");
            }

            return new PAResult("Started. No active page available.");
        }
        catch (Exception ex)
        {
            return new PAResult(ex.Message);
        }
    }

    public Task<PAResult> StopAsync()
    {
        State = PluginState.Stopped;
        return Task.FromResult(new PAResult("Stopped."));
    }

    public Task<PAResult> PauseAsync() => Task.FromResult(new PAResult("Pause not supported."));

    public Task<PAResult> ResumeAsync() => Task.FromResult(new PAResult("Resume not supported."));

    public ValueTask DisposeAsync()
    {
        // No unmanaged resources in this minimal example.
        return ValueTask.CompletedTask;
    }
}
