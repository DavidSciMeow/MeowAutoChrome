using MeowAutoChrome.Contracts;
using MeowAutoChrome.Contracts.Attributes;

namespace MeowAutoChrome.ExamplePlugin;

[Plugin("example.minimal", "Example Minimal Plugin", Description = "示例：基于最小 Contracts 表面实现的插件。")]
public sealed class MinimalExamplePlugin : IPlugin, IAsyncDisposable
{
    public PluginState State { get; private set; } = PluginState.Stopped;

    public bool SupportsPause => false;

    public IPluginContext? HostContext { get; set; }

    public async Task<IResult> StartAsync()
    {
        State = PluginState.Running;

        try
        {
            // If host provided an active page, report its title (as before)
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

                return Result.Ok(new { title });
            }

            // Demonstrate requesting a fresh browser instance from the host.
            if (HostContext is not null)
            {
                var opts = new BrowserCreationOptions(OwnerId: "example.minimal", UserDataDirectory: null, BrowserType: "chromium", Headless: true, UserAgent: null, DisplayName: "ExamplePluginInstance");
                var instanceId = await HostContext.RequestNewBrowserInstanceAsync(opts, HostContext.CancellationToken);
                if (!string.IsNullOrWhiteSpace(instanceId))
                {
                    return Result.Ok(new { instanceId });
                }

                return Result.Fail("Host refused to create a new browser instance.");
            }

            return Result.Ok(new { message = "Started. No active page available and no host context to request new one." });
        }
        catch (Exception ex)
        {
            return Result.Fail(ex.Message);
        }
    }

    public Task<IResult> StopAsync()
    {
        State = PluginState.Stopped;
        return Task.FromResult<IResult>(Result.Ok(new { message = "Stopped." }));
    }

    public Task<IResult> PauseAsync() => Task.FromResult<IResult>(Result.Ok(new { message = "Pause not supported." }));

    public Task<IResult> ResumeAsync() => Task.FromResult<IResult>(Result.Ok(new { message = "Resume not supported." }));

    public ValueTask DisposeAsync()
    {
        // No unmanaged resources in this minimal example.
        return ValueTask.CompletedTask;
    }

    // Extra demo actions to illustrate different return shapes for plugins
    [PAction(Name = "ReturnObject")]
    public async Task<IResult> ReturnObjectAsync()
    {
        await Task.Delay(10);
        return Result.Ok(new { now = DateTime.UtcNow, message = "returned object" });
    }

    [PAction(Name = "ReturnGeneric")]
    public Task<Result<int>> ReturnGenericAsync()
    {
        return Task.FromResult(Result<int>.Ok(42));
    }

    [PAction(Name = "VoidTask")]
    public async Task VoidTaskAsync()
    {
        await Task.Delay(10);
        // no return
    }

    [PAction(Name = "Throw")]
    public Task<IResult> ThrowAsync()
    {
        throw new InvalidOperationException("示例异常");
    }

    // Additional demo actions to illustrate more return shapes and HostContext usage
    [PAction(Name = "ReturnPrimitive")]
    public Task<int> ReturnPrimitiveAsync()
    {
        return Task.FromResult(7);
    }

    [PAction(Name = "ValueTaskResult")]
    public ValueTask<Result<string>> ValueTaskResultAsync()
    {
        return new ValueTask<Result<string>>(Result<string>.Ok("value-task-result"));
    }

    [PAction(Name = "RequestInstanceWithArgs")]
    public async Task<IResult> RequestInstanceWithArgsAsync()
    {
        if (HostContext is null) return Result.Fail("No host context available");

        var opts = new BrowserCreationOptions(
            OwnerId: HostContext.PluginId,
            UserDataDirectory: null,
            BrowserType: "chromium",
            Headless: true,
            UserAgent: "ExamplePlugin/1.0",
            DisplayName: "Example-With-Args",
            Args: new[] { "--no-sandbox", "--disable-setuid-sandbox" }
        );

        var instanceId = await HostContext.RequestNewBrowserInstanceAsync(opts, HostContext.CancellationToken);
        if (string.IsNullOrWhiteSpace(instanceId))
            return Result.Fail("Host refused to create a new browser instance.");

        return Result.Ok(new { instanceId });
    }

    [PAction(Name = "EvaluateActivePage")]
    public async Task<IResult> EvaluateActivePageAsync()
    {
        if (HostContext?.ActivePage is null) return Result.Fail("No active page available");

        try
        {
            // Read document title via Playwright evaluate
            var title = await HostContext.ActivePage.EvaluateAsync<string>("() => document.title");
            return Result.Ok(new { title });
        }
        catch (Exception ex)
        {
            return Result.Fail(ex.Message);
        }
    }

    [PAction(Name = "GetInstanceInfo")]
    public async Task<IResult> GetInstanceInfoAsync()
    {
        if (HostContext is null) return Result.Fail("No host context available");

        // Expect instanceId to be provided via HostContext.Arguments["instanceId"]
        if (!HostContext.Arguments.TryGetValue("instanceId", out var iid) || string.IsNullOrWhiteSpace(iid))
            return Result.Fail("Missing required argument 'instanceId' in HostContext.Arguments");

        try
        {
            var info = await HostContext.GetBrowserInstanceInfoAsync(iid!, HostContext.CancellationToken);
            if (info is null) return Result.Fail("Instance not found");
            return Result.Ok(info);
        }
        catch (Exception ex)
        {
            return Result.Fail(ex.Message);
        }
    }
}
