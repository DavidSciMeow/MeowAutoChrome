using MeowAutoChrome.Contracts;
using MeowAutoChrome.Contracts.Attributes;

namespace MeowAutoChrome.ExamplePlugin;

[Plugin("example.minimal", "Example Minimal Plugin", Description = "示例：展示新的 Host/Browser facade 契约。")]
/// <summary>
/// 示例最小插件实现，演示如何通过 Host facade 管理自己名下的浏览器实例，并访问当前实例上的页面与浏览器上下文。<br/>
/// Minimal example plugin implementation demonstrating how to manage plugin-owned browser instances via the Host facade and access pages/browser context on the current instance.
/// </summary>
public sealed class MinimalExamplePlugin : PluginBase, IAsyncDisposable
{
    private const string DefaultInstanceId = "ExamplePluginInstance";

    /// <summary>
    /// 启动插件。若当前已存在该插件名下实例则优先复用，否则由宿主创建一个新实例。宿主在返回成功后会自行切换运行状态。<br/>
    /// Start the plugin. Reuse a plugin-owned instance when possible; otherwise ask the host to create a new one. The host updates runtime state after success is returned.
    /// </summary>
    public override async Task<IResult> StartAsync()
    {
        if (HostContext is null) return Result.Fail("No host context available");

        var existing = Instance;
        var instance = existing;
        if (instance is null)
        {
            instance = Instances.FirstOrDefault(candidate => string.Equals(candidate.InstanceId, DefaultInstanceId, StringComparison.Ordinal))
                ?? await CreateBrowserInstanceAsync(new BrowserCreationOptions(
                    DisplayName: "Example Plugin Instance",
                    Headless: false,
                    RequestedInstanceId: DefaultInstanceId));
        }

        if (instance is null) return Result.Fail("Host did not provide or create a browser instance.");

        await SelectBrowserInstanceAsync(instance.InstanceId);
        await PublishUpdateAsync("插件已启动。", new Dictionary<string, string?>
        {
            ["pluginId"] = Host.PluginId,
            ["state"] = State.ToString(),
            ["instanceId"] = instance.InstanceId,
            ["baseAddress"] = Host.BaseAddress,
            ["ownedInstanceCount"] = Instances.Count.ToString(),
            ["reused"] = (existing is not null).ToString()
        });

        return Result.Ok(new
        {
            instanceId = instance.InstanceId,
            instance.DisplayName,
            hostBaseAddress = Host.BaseAddress,
            pageCount = instance.Pages.Count,
            reused = existing is not null
        });
    }

    /// <summary>
    /// 停止插件。示例实现会请求宿主关闭当前插件名下的所有浏览器实例，宿主在返回成功后会将状态置为 Stopped。<br/>
    /// Stop the plugin. This example asks the host to close all browser instances owned by the plugin, and the host sets the state to Stopped after success is returned.
    /// </summary>
    public override async Task<IResult> StopAsync()
    {
        if (HostContext is null) return Result.Fail("No host context available");

        var owned = Instances.ToArray();
        var closed = 0;
        foreach (var instance in owned)
        {
            if (await CloseBrowserInstanceAsync(instance.InstanceId)) closed++;
        }

        await PublishUpdateAsync("插件已停止。", new Dictionary<string, string?>
        {
            ["state"] = State.ToString(),
            ["closedInstances"] = closed.ToString()
        });

        return Result.Ok(new { closedInstances = closed });
    }
    public override Task<IResult> PauseAsync() => Task.FromResult<IResult>(Result.Fail("Pause not supported."));
    public override Task<IResult> ResumeAsync() => Task.FromResult<IResult>(Result.Fail("Resume not supported."));

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>
    /// 返回一个对象结果，同时演示消息发布。<br/>
    /// Return an object result while demonstrating host-side message publishing.
    /// </summary>
    [PAction(Name = "ReturnObject")]
    public async Task<IResult> ReturnObjectAsync() => Result.Ok(new { now = DateTime.UtcNow, message = "returned object" });

    [PAction(Name = "ReturnGeneric")]
    /// <summary>
    /// 返回一个泛型结果。<br/>
    /// Return a generic result.
    /// </summary>
    public Task<Result<int>> ReturnGenericAsync() => Task.FromResult(Result<int>.Ok(42));

    [PAction(Name = "VoidTask")]
    /// <summary>
    /// 返回无结果异步任务。<br/>
    /// Return an asynchronous task without a result payload.
    /// </summary>
    public async Task VoidTaskAsync() => await Task.Delay(10, CancellationToken);

    [PAction(Name = "Throw")]
    /// <summary>
    /// 抛出一个示例异常，用于观察宿主错误处理。<br/>
    /// Throw a sample exception so the host's error handling can be observed.
    /// </summary>
    public Task<IResult> ThrowAsync() => throw new InvalidOperationException("示例异常");

    [PAction(Name = "ReturnPrimitive")]
    /// <summary>
    /// 返回基础类型结果。<br/>
    /// Return a primitive result.
    /// </summary>
    public Task<int> ReturnPrimitiveAsync() => Task.FromResult(7);

    [PAction(Name = "ValueTaskResult")]
    /// <summary>
    /// 返回 ValueTask 包装的字符串结果。<br/>
    /// Return a string result wrapped in ValueTask.
    /// </summary>
    public ValueTask<Result<string>> ValueTaskResultAsync() => new(Result<string>.Ok("value-task-result"));

    [PAction(Name = "EvaluateActivePage")]
    /// <summary>
    /// 在当前活动页面上执行 JavaScript 并返回标题。<br/>
    /// Execute JavaScript on the current active page and return the title.
    /// </summary>
    public async Task<IResult> EvaluateActivePageAsync()
    {
        if (ActivePage is null)
            return Result.Fail("No active page available");

        await PublishUpdateAsync("开始读取当前页面标题。", null);
        var title = await ActivePage.EvaluateAsync<string>("() => document.title");
        await PublishUpdateAsync("已读取页面标题。", new Dictionary<string, string?>
        {
            ["title"] = title,
            ["baseAddress"] = Host?.BaseAddress
        });

        return Result.Ok(new { title });
    }

    [PAction(Name = "OpenBaiduAndGetTitle")]
    /// <summary>
    /// 在当前插件拥有的实例中打开百度并返回标题。若当前没有可用实例，则先请求宿主创建一个。<br/>
    /// Open Baidu in a plugin-owned instance and return the title. If no usable instance exists, ask the host to create one first.
    /// </summary>
    public async Task<IResult> OpenBaiduAndGetTitleAsync()
    {
        if (HostContext is null)
            return Result.Fail("No host context available");

        var instance = Instance
            ?? Instances.FirstOrDefault(candidate => string.Equals(candidate.InstanceId, DefaultInstanceId, StringComparison.Ordinal))
            ?? await CreateBrowserInstanceAsync(new BrowserCreationOptions(
                DisplayName: "Example Plugin Instance",
                Headless: false,
                RequestedInstanceId: DefaultInstanceId));

        if (instance?.BrowserContext is null)
            return Result.Fail("No browser context is available for the selected instance.");

        await SelectBrowserInstanceAsync(instance.InstanceId);
        await PublishUpdateAsync("准备打开百度首页。", new Dictionary<string, string?>
        {
            ["url"] = "https://www.baidu.com",
            ["instanceId"] = instance.InstanceId
        });

        var page = await instance.BrowserContext.NewPageAsync();
        try
        {
            await PublishUpdateAsync("新页面已打开，开始导航。", new Dictionary<string, string?>
            {
                ["instanceId"] = instance.InstanceId,
                ["step"] = "navigate"
            });
            await page.GotoAsync("https://www.baidu.com");
            var title = await page.TitleAsync();
            await ToastAsync("导航完成并取得标题。", new Dictionary<string, string?>
            {
                ["instanceId"] = instance.InstanceId,
                ["title"] = title
            });
            return Result.Ok(new { instanceId = instance.InstanceId, title });
        }
        finally
        {
            //try { await page.CloseAsync(); } catch { }
        }
    }
}