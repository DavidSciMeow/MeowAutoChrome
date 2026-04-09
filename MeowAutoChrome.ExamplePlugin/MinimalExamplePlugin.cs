using MeowAutoChrome.Contracts;
using MeowAutoChrome.Contracts.Attributes;
using Microsoft.Playwright;

namespace MeowAutoChrome.ExamplePlugin;

[Plugin("example.minimal", "Example Minimal Plugin", Description = "示例：基于最小 Contracts 表面实现的插件。")]
/// <summary>
/// 示例最小插件实现，演示如何使用 Contracts API 与宿主交互。<br/>
/// Minimal example plugin implementation demonstrating how to interact with the host via the Contracts API.
/// </summary>
/// <remarks>
/// 提供若干示例动作（PAction）以展示不同的返回类型与 HostContext 用法。<br/>
/// Provides several example actions (PAction) demonstrating different return shapes and HostContext usage.
/// </remarks>
public sealed class MinimalExamplePlugin : IPlugin, IAsyncDisposable
{
    private Task PublishUpdateAsync(string? message, IReadOnlyDictionary<string, string?>? data = null, bool openModal = false) => HostContext?.PublishUpdateAsync(message, data, openModal) ?? Task.CompletedTask;

    private async Task<(string? CurrentOwnedInstanceId, IReadOnlyList<string> OwnedInstanceIds)> GetOwnedInstancesAsync()
    {
        if (HostContext is null)
            return (null, Array.Empty<string>());

        var ownedInstanceIds = await HostContext.GetPluginInstanceIdsAsync(HostContext.CancellationToken);
        var currentOwnedInstanceId = ownedInstanceIds.FirstOrDefault(id => string.Equals(id, HostContext.BrowserInstanceId, StringComparison.Ordinal));
        return (currentOwnedInstanceId, ownedInstanceIds);
    }

    /// <summary>
    /// 插件状态。<br/>
    /// Plugin state.
    /// </summary>
    public PluginState State { get; private set; } = PluginState.Stopped;

    /// <summary>
    /// 是否支持暂停。<br/>
    /// Whether pause is supported.
    /// </summary>
    public bool SupportsPause => false;

    /// <summary>
    /// 注入的宿主上下文，宿主在调用插件动作前设置该属性。<br/>
    /// Injected host context; set by the host before invoking plugin actions.
    /// </summary>
    public IPluginContext? HostContext { get; set; }

    /// <summary>
    /// 启动插件并执行示例初始化/示范逻辑。<br/>
    /// Start the plugin and perform example initialization/demo logic.
    /// </summary>
    /// <returns>返回操作结果（可能包含额外数据）。<br/>Returns an operation result (may include additional data).</returns>
    public async Task<IResult> StartAsync()
    {
        if (HostContext is null) return Result.Fail("No host context available");

        State = PluginState.Running;
        await PublishUpdateAsync("插件已启动。", new Dictionary<string, string?>
        {
            ["state"] = State.ToString(),
            ["pluginId"] = "example.minimal"
        });

        try
        {
            var (currentOwnedInstanceId, ownedInstanceIds) = await GetOwnedInstancesAsync();
            if (!string.IsNullOrWhiteSpace(currentOwnedInstanceId))
            {
                await PublishUpdateAsync("已复用当前浏览器实例。", new Dictionary<string, string?>
                {
                    ["instanceId"] = currentOwnedInstanceId,
                    ["displayName"] = "ExamplePluginInstance"
                });
                return Result.Ok(new { instanceId = currentOwnedInstanceId, reused = true });
            }

            if (ownedInstanceIds.Count > 0)
                return Result.Fail($"Plugin has existing instances ({string.Join(", ", ownedInstanceIds)}), but none is the current selected instance.");

            return Result.Fail("No existing browser instance owned by this plugin is available to reuse.");
        }
        catch (Exception ex)
        {
            return Result.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 停止插件并释放资源。<br/>
    /// Stop the plugin and release resources.
    /// </summary>
    /// <returns>返回操作结果。<br/>Returns operation result.</returns>
    public async Task<IResult> StopAsync()
    {
        if (HostContext is null) return Result.Fail("No host context available");
        while (true)
        {
            if (HostContext.ActivePage is IPage a) await a.CloseAsync();
            else break;
        }
        State = PluginState.Stopped;
        _ = PublishUpdateAsync("插件已停止。", new Dictionary<string, string?>
        {
            ["state"] = State.ToString()
        });
        return Result.Ok(new { message = "Stopped." });
    }

    /// <summary>
    /// 将插件置于暂停状态（此示例中不支持）。<br/>
    /// Pause the plugin (not supported in this example).
    /// </summary>
    /// <returns>返回操作结果 / returns an operation result.</returns>
    public Task<IResult> PauseAsync() => Task.FromResult<IResult>(Result.Ok(new { message = "Pause not supported." }));

    /// <summary>
    /// 从暂停状态恢复插件（此示例中不支持）。<br/>
    /// Resume the plugin from paused state (not supported in this example).
    /// </summary>
    /// <returns>返回操作结果 / returns an operation result.</returns>
    public Task<IResult> ResumeAsync() => Task.FromResult<IResult>(Result.Ok(new { message = "Resume not supported." }));

    /// <summary>
    /// 异步释放插件所持有的非托管资源（若有）。<br/>
    /// Asynchronously dispose unmanaged resources held by the plugin (if any).
    /// </summary>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    // Extra demo actions to illustrate different return shapes for plugins
    [PAction(Name = "ReturnObject")]
    /// <summary>
    /// 示例动作：返回一个匿名对象（演示非空对象返回）。<br/>
    /// Demo action returning an anonymous object.
    /// </summary>
    /// <returns>返回封装的结果对象。<br/>Returns the wrapped result object.</returns>
    public async Task<IResult> ReturnObjectAsync()
    {
        await Task.Delay(10);
        await PublishUpdateAsync("正在返回对象结果。", new Dictionary<string, string?>
        {
            ["kind"] = "object"
        });
        return Result.Ok(new { now = DateTime.UtcNow, message = "returned object" });
    }

    [PAction(Name = "ReturnGeneric")]
    /// <summary>
    /// 示例动作：返回泛型 `Result<int>`。<br/>
    /// Demo action returning a generic `Result<int>`.
    /// </summary>
    /// <returns>封装的整型结果。<br/>Wrapped integer result.</returns>
    public Task<Result<int>> ReturnGenericAsync() => Task.FromResult(Result<int>.Ok(42));

    [PAction(Name = "VoidTask")]
    /// <summary>
    /// 示例动作：返回无结果的异步任务。<br/>
    /// Demo action returning a void Task.
    /// </summary>
    public async Task VoidTaskAsync() => await Task.Delay(10);// no return

    [PAction(Name = "Throw")]
    /// <summary>
    /// 示例动作：抛出异常以演示宿主如何处理插件异常。<br/>
    /// Demo action that throws an exception to illustrate host error handling.
    /// </summary>
    /// <returns>此方法将抛出异常而不会返回值。<br/>This method throws and does not return a value.</returns>
    public Task<IResult> ThrowAsync() => throw new InvalidOperationException("示例异常");

    // Additional demo actions to illustrate more return shapes and HostContext usage
    [PAction(Name = "ReturnPrimitive")]
    /// <summary>
    /// 示例动作：返回基础类型。<br/>
    /// Demo action returning a primitive value.
    /// </summary>
    /// <returns>返回一个整数。<br/>Returns an integer.</returns>
    public Task<int> ReturnPrimitiveAsync() => Task.FromResult(7);

    [PAction(Name = "ValueTaskResult")]
    /// <summary>
    /// 示例动作：返回 `ValueTask` 包裹的泛型结果。<br/>
    /// Demo action returning a generic result wrapped in `ValueTask`.
    /// </summary>
    /// <returns>封装的字符串结果。<br/>Wrapped string result.</returns>
    public ValueTask<Result<string>> ValueTaskResultAsync() => new(Result<string>.Ok("value-task-result"));

    [PAction(Name = "RequestInstanceWithArgs")]
    /// <summary>
    /// 示例动作：向宿主请求一个新的浏览器实例并传递启动参数。<br/>
    /// Demo action requesting a new browser instance from the host with startup args.
    /// </summary>
    /// <returns>返回新创建实例的 ID 或失败信息。<br/>Returns the new instance id or failure information.</returns>
    public async Task<IResult> RequestInstanceWithArgsAsync()
    {
        if (HostContext is null) return Result.Fail("No host context available");

        const string requestedInstanceId = "Example-With-Args";

        var (_, ownedInstanceIds) = await GetOwnedInstancesAsync();
        var reusableInstanceId = ownedInstanceIds.FirstOrDefault(id => string.Equals(id, requestedInstanceId, StringComparison.Ordinal));
        if (!string.IsNullOrWhiteSpace(reusableInstanceId))
        {
            var existing = await HostContext.GetBrowserInstanceInfoAsync(reusableInstanceId, HostContext.CancellationToken);
            await PublishUpdateAsync("复用已存在的浏览器实例。", new Dictionary<string, string?>
            {
                ["instanceId"] = reusableInstanceId,
                ["mode"] = "with-args-reused"
            });

            return Result.Ok(new
            {
                instanceId = reusableInstanceId,
                reused = true,
                displayName = existing?.DisplayName
            });
        }

        await PublishUpdateAsync("未找到可复用的浏览器实例。", new Dictionary<string, string?>
        {
            ["requestedInstanceId"] = requestedInstanceId,
            ["mode"] = "with-args-missing"
        });

        return Result.Fail($"No reusable browser instance '{requestedInstanceId}' exists for plugin '{HostContext.PluginId}'.");
    }

    [PAction(Name = "EvaluateActivePage")]
    /// <summary>
    /// 示例动作：在当前活动页面上执行 Playwright 的 evaluate 调用并返回结果。<br/>
    /// Demo action evaluating JavaScript on the active page via Playwright and returning the result.
    /// </summary>
    /// <returns>返回页面上脚本执行的结果或失败信息。<br/>Returns the script evaluation result or failure information.</returns>
    public async Task<IResult> EvaluateActivePageAsync()
    {
        if (HostContext?.ActivePage is null) return Result.Fail("No active page available");

        await PublishUpdateAsync("开始读取当前页面标题。", null);

        try
        {
            // Read document title via Playwright evaluate
            var title = await HostContext.ActivePage.EvaluateAsync<string>("() => document.title");
            await PublishUpdateAsync("已读取页面标题。", new Dictionary<string, string?>
            {
                ["title"] = title
            });
            return Result.Ok(new { title });
        }
        catch (Exception ex)
        {
            return Result.Fail(ex.Message);
        }
    }

    [PAction(Name = "GetInstanceInfo")]
    /// <summary>
    /// 示例动作：根据提供的 instanceId 查询宿主上的浏览器实例信息。<br/>
    /// Demo action to query browser instance info on the host for the provided instanceId.
    /// </summary>
    /// <param name="instanceId">要查询的实例 ID（由 HostContext.Arguments 提供）。<br/>Instance id to query (expected via HostContext.Arguments).</param>
    /// <returns>返回实例信息或失败结果。<br/>Returns instance information or a failure result.</returns>
    public async Task<IResult> GetInstanceInfoAsync(
        [PInput(Name = "instanceId", Required = true)] string instanceId
        )
    {
        if (HostContext is null) return Result.Fail("No host context available");

        // Expect instanceId to be provided via HostContext.Arguments["instanceId"]
        if (string.IsNullOrWhiteSpace(instanceId))
            return Result.Fail("Missing required argument 'instanceId' in HostContext.Arguments");

        try
        {
            var info = await HostContext.GetBrowserInstanceInfoAsync(instanceId, HostContext.CancellationToken);
            if (info is null) return Result.Fail("Instance not found");
            return Result.Ok(info);
        }
        catch (Exception ex)
        {
            return Result.Fail(ex.Message);
        }
    }

    [PAction(Name = "OpenBaiduAndGetTitle")]
    /// <summary>
    /// 请求宿主创建新实例、在浏览器上下文中打开新页面并导航到 baidu.com，然后返回标题。<br/>
    /// Request the host to create a new browser instance, open a page in the browser context, navigate to baidu.com and return the page title.
    /// </summary>
    public async Task<IResult> OpenBaiduAndGetTitleAsync()
    {
        if (HostContext is null) return Result.Fail("No host context available");

        var (currentOwnedInstanceId, ownedInstanceIds) = await GetOwnedInstancesAsync();
        if (string.IsNullOrWhiteSpace(currentOwnedInstanceId))
        {
            if (ownedInstanceIds.Count > 0)
                return Result.Fail($"Plugin has reusable instances ({string.Join(", ", ownedInstanceIds)}), but none is currently selected.");

            return Result.Fail("No existing browser instance owned by this plugin is available to reuse.");
        }

        var browserContext = HostContext.BrowserContext;
        if (browserContext is null) return Result.Fail("Host did not provide a browser context for the current reused instance.");

        await PublishUpdateAsync("准备打开百度首页。", new Dictionary<string, string?>
        {
            ["url"] = "https://www.baidu.com",
            ["instanceId"] = currentOwnedInstanceId
        });

        try
        {
            var page = await browserContext.NewPageAsync();
            try
            {
                await PublishUpdateAsync("新页面已打开，开始导航。", new Dictionary<string, string?>
                {
                    ["instanceId"] = currentOwnedInstanceId,
                    ["step"] = "navigate"
                });
                await page.GotoAsync("https://www.baidu.com");
                var title = await page.TitleAsync();
                await PublishUpdateAsync("导航完成并取得标题。", new Dictionary<string, string?>
                {
                    ["instanceId"] = currentOwnedInstanceId,
                    ["title"] = title
                }, true);
                return Result.Ok(new { instanceId = currentOwnedInstanceId, title, reused = true });
            }
            finally
            {
                try { await page.CloseAsync(); } catch { }
            }
        }
        catch (Exception ex)
        {
            return Result.Fail(ex.Message);
        }
    }
}
