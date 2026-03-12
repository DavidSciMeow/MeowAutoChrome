using MeowAutoChrome.Contracts;

namespace MeowAutoChrome.ExamplePlugin;

[BrowserPlugin("example.page-title", "Example 标题插件", Description = "示例插件：读取当前活动网页标题和地址。")]
public sealed class ExamplePageTitlePlugin : IBrowserPlugin
{
    public BrowserPluginState State { get; private set; } = BrowserPluginState.Stopped;

    public bool SupportsPause => true;

    public Task<BrowserPluginActionResult> StartAsync(IReadOnlyDictionary<string, string?> arguments, IBrowserPluginContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (State == BrowserPluginState.Running)
            return Task.FromResult(new BrowserPluginActionResult("插件已处于运行中。", BuildStateData()));

        State = BrowserPluginState.Running;
        return Task.FromResult(new BrowserPluginActionResult("Example 插件已启动。", BuildStateData()));
    }

    public Task<BrowserPluginActionResult> StopAsync(IBrowserPluginContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        State = BrowserPluginState.Stopped;
        return Task.FromResult(new BrowserPluginActionResult("Example 插件已停止。", BuildStateData()));
    }

    public Task<BrowserPluginActionResult> PauseAsync(IBrowserPluginContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (State != BrowserPluginState.Running)
            return Task.FromResult(new BrowserPluginActionResult("只有运行中的插件才能暂停。", BuildStateData()));

        State = BrowserPluginState.Paused;
        return Task.FromResult(new BrowserPluginActionResult("Example 插件已暂停。", BuildStateData()));
    }

    public Task<BrowserPluginActionResult> ResumeAsync(IBrowserPluginContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (State != BrowserPluginState.Paused)
            return Task.FromResult(new BrowserPluginActionResult("只有暂停中的插件才能恢复。", BuildStateData()));

        State = BrowserPluginState.Running;
        return Task.FromResult(new BrowserPluginActionResult("Example 插件已恢复。", BuildStateData()));
    }

    [BrowserPluginAction("read-title", "读取网页标题", Description = "通过宿主开放的 Contract 能力读取当前活动页标题。")]
    [BrowserPluginInput("prefix", "结果前缀", Description = "可选，自定义返回消息前缀。", DefaultValue = "当前页面标题")]
    public async Task<BrowserPluginActionResult> ReadTitleAsync(IBrowserPluginContext context, IReadOnlyDictionary<string, string?> arguments, CancellationToken cancellationToken)
    {
        if (State != BrowserPluginState.Running)
            return new BrowserPluginActionResult("请先启动插件，再执行导出函数。", BuildStateData());

        if (!await context.HasCapabilityAsync(BrowserPluginCapabilities.PageTitle, cancellationToken))
            return new BrowserPluginActionResult("宿主尚未开放页面标题读取能力。");

        var title = await context.GetPageTitleAsync(cancellationToken);
        var url = await context.GetCurrentUrlAsync(cancellationToken);
        var prefix = arguments.TryGetValue("prefix", out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : "当前页面标题";

        return new BrowserPluginActionResult(
            string.IsNullOrWhiteSpace(title) ? "当前页面没有可用标题。" : $"{prefix}：{title}",
            new Dictionary<string, string?>
            {
                ["title"] = title,
                ["url"] = url,
                ["apiVersion"] = context.ApiVersion,
                ["state"] = State.ToString(),
            });
    }

    private IReadOnlyDictionary<string, string?> BuildStateData()
        => new Dictionary<string, string?>
        {
            ["state"] = State.ToString(),
        };
}

