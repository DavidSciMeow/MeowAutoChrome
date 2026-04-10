using MeowAutoChrome.Contracts;
using MeowAutoChrome.Contracts.Attributes;

namespace MeowAutoChrome.ExamplePlugin;

/// <summary>
/// 示例最小插件实现，演示如何通过 Host facade 管理自己名下的浏览器实例，并访问当前实例上的页面与浏览器上下文。<br/>
/// Minimal example plugin implementation demonstrating how to manage plugin-owned browser instances via the Host facade and access pages/browser context on the current instance.
/// </summary>
[Plugin("example.minimal", "Example Minimal Plugin", Description = "示例：展示新的 Host/Browser facade 契约。")]
public sealed class MinimalExamplePlugin : PluginBase, IAsyncDisposable
{
    private const string DefaultInstanceId = "ExamplePluginInstance";

    public enum ExampleChoice
    {
        Alpha,
        Beta,
        Gamma
    }

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
        await MessageAsync("插件已启动。", new Dictionary<string, string?>
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

        await MessageAsync("插件已停止。", new Dictionary<string, string?>
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

    /// <summary>
    /// 返回一个泛型结果。<br/>
    /// Return a generic result.
    /// </summary>
    [PAction(Name = "ReturnGeneric")]
    public Task<Result<int>> ReturnGenericAsync() => Task.FromResult(Result<int>.Ok(42));

    /// <summary>
    /// 返回无结果异步任务。<br/>
    /// Return an asynchronous task without a result payload.
    /// </summary>
    [PAction(Name = "VoidTask")]
    public async Task VoidTaskAsync() => await Task.Delay(10, CancellationToken);

    /// <summary>
    /// 抛出一个示例异常，用于观察宿主错误处理。<br/>
    /// Throw a sample exception so the host's error handling can be observed.
    /// </summary>
    [PAction(Name = "Throw")]
    public Task<IResult> ThrowAsync() => throw new InvalidOperationException("示例异常");

    /// <summary>
    /// 返回基础类型结果。<br/>
    /// Return a primitive result.
    /// </summary>
    [PAction(Name = "ReturnPrimitive")]
    public Task<int> ReturnPrimitiveAsync() => Task.FromResult(7);

    /// <summary>
    /// 返回 ValueTask 包装的字符串结果。<br/>
    /// Return a string result wrapped in ValueTask.
    /// </summary>
    [PAction(Name = "ValueTaskResult")]
    public ValueTask<Result<string>> ValueTaskResultAsync() => new(Result<string>.Ok("value-task-result"));

    /// <summary>
    /// 接收可选文本输入，演示 PInput 触发参数模态框以及 textarea 渲染。<br/>
    /// Accept optional text input to demonstrate PInput-driven argument modal rendering with a textarea.
    /// </summary>
    [PAction(Name = "EchoText")]
    public Task<IResult> EchoTextAsync(
        [PInput(Label = "文本", Description = "可留空，支持多行输入。", InputType = "textarea", Rows = 8)]
        string? text = null)
        => Task.FromResult<IResult>(Result.Ok(new
        {
            text = text ?? string.Empty,
            length = text?.Length ?? 0
        }));

    /// <summary>
    /// 展示当前可识别的所有输入类型，便于手工验证参数窗渲染与参数绑定。<br/>
    /// Showcase every currently recognized input type so the argument modal and parameter binding can be tested manually.
    /// </summary>
    [PAction(Name = "InputTypeShowcase")]
    public Task<IResult> InputTypeShowcaseAsync(
        [PInput(Label = "单行文本", Description = "默认渲染为 text。")]
        string text,
        [PInput(Label = "多行文本", Description = "使用 Multiline 语义糖渲染为 textarea。", Multiline = true, Rows = 6)]
        string? multilineText,
        [PInput(Label = "数字", Description = "decimal 会自动渲染为 number。")]
        decimal amount,
        [PInput(Label = "日期时间", Description = "DateTime 会自动渲染为 datetime-local。")]
        DateTime scheduledAt,
        [PInput(Label = "唯一标识", Description = "Guid 会自动渲染为 guid 文本输入。")]
        Guid requestId,
        [PInput(Label = "布尔开关", Description = "bool 会自动渲染为 checkbox。")]
        bool enabled,
        [PInput(Label = "枚举选择", Description = "enum 会自动渲染为 select。")]
        ExampleChoice choice = ExampleChoice.Alpha)
        => Task.FromResult<IResult>(Result.Ok(new
        {
            text,
            multilineText = multilineText ?? string.Empty,
            amount,
            scheduledAt,
            requestId,
            enabled,
            choice = choice.ToString()
        }));

    /// <summary>
    /// 在当前活动页面上执行 JavaScript 并返回标题。<br/>
    /// Execute JavaScript on the current active page and return the title.
    /// </summary>
    [PAction(Name = "EvaluateActivePage")]
    public async Task<IResult> EvaluateActivePageAsync()
    {
        if (ActivePage is null)
            return Result.Fail("No active page available");

        await MessageAsync("开始读取当前页面标题。");
        var title = await ActivePage.EvaluateAsync<string>("() => document.title");
        await MessageAsync($"已读取页面标题。通过JS {title}");

        return Result.Ok(new { title });
    }

    /// <summary>
    /// 在当前插件拥有的实例中打开百度并返回标题。若当前没有可用实例，则先请求宿主创建一个。<br/>
    /// Open Baidu in a plugin-owned instance and return the title. If no usable instance exists, ask the host to create one first.
    /// </summary>
    [PAction(Name = "OpenBaiduAndGetTitle")]
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
        await ToastAsync("准备打开百度首页。");

        var page = await instance.BrowserContext.NewPageAsync();
        try
        {
            await ToastAsync("新页面已打开，开始导航。");
            await page.GotoAsync("https://www.baidu.com");
            var title = await page.TitleAsync();
            await ToastAsync($"导航完成并取得标题。{title}");
            return Result.Ok(new { instanceId = instance.InstanceId, title });
        }
        finally
        {
            //try { await page.CloseAsync(); } catch { }
        }
    }
}