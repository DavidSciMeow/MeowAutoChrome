using MeowAutoChrome.Contracts;
using Microsoft.Playwright;

namespace MeowAutoChrome.ExamplePlugin;

[BrowserPlugin("example.page-title", "Example 标题插件", Description = "示例插件：读取当前活动网页标题和地址。")]
public sealed class ExamplePageTitlePlugin : IBrowserPlugin
{
    public BrowserPluginState State { get; private set; } = BrowserPluginState.Stopped;

    public bool SupportsPause => true;

    public Task<BrowserPluginActionResult> StartAsync(IReadOnlyDictionary<string, string?> arguments, IBrowserContext browserContext, IPage? activePage, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (State == BrowserPluginState.Running)
            return Task.FromResult(new BrowserPluginActionResult("插件已处于运行中。", BuildStateData()));

        State = BrowserPluginState.Running;
        return Task.FromResult(new BrowserPluginActionResult("Example 插件已启动。", BuildStateData()));
    }

    public Task<BrowserPluginActionResult> StopAsync(IBrowserContext browserContext, IPage? activePage, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        State = BrowserPluginState.Stopped;
        return Task.FromResult(new BrowserPluginActionResult("Example 插件已停止。", BuildStateData()));
    }

    public Task<BrowserPluginActionResult> PauseAsync(IBrowserContext browserContext, IPage? activePage, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (State != BrowserPluginState.Running)
            return Task.FromResult(new BrowserPluginActionResult("只有运行中的插件才能暂停。", BuildStateData()));

        State = BrowserPluginState.Paused;
        return Task.FromResult(new BrowserPluginActionResult("Example 插件已暂停。", BuildStateData()));
    }

    public Task<BrowserPluginActionResult> ResumeAsync(IBrowserContext browserContext, IPage? activePage, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (State != BrowserPluginState.Paused)
            return Task.FromResult(new BrowserPluginActionResult("只有暂停中的插件才能恢复。", BuildStateData()));

        State = BrowserPluginState.Running;
        return Task.FromResult(new BrowserPluginActionResult("Example 插件已恢复。", BuildStateData()));
    }

    [BrowserPluginAction("read-title", "读取网页标题", Description = "直接通过 IPage 读取 document.title。")]
    [BrowserPluginInput("prefix", "结果前缀", Description = "可选，自定义返回消息前缀。", DefaultValue = "当前页面标题")]
    public async Task<BrowserPluginActionResult> ReadTitleAsync(IBrowserContext browserContext, IPage? activePage, IReadOnlyDictionary<string, string?> arguments, CancellationToken cancellationToken)
    {
        if (State != BrowserPluginState.Running)
            return new BrowserPluginActionResult("请先启动插件，再执行导出函数。", BuildStateData());

        if (activePage is null)
            return new BrowserPluginActionResult("当前没有活动页面。", BuildStateData());

        var prefix = arguments.TryGetValue("prefix", out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : "当前页面标题";
        var title = await activePage.EvaluateAsync<string?>("() => document?.title ?? null");
        var url = activePage.Url;

        return new BrowserPluginActionResult(
            string.IsNullOrWhiteSpace(title) ? "当前页面没有可用标题。" : $"{prefix}：{title}",
            new Dictionary<string, string?>
            {
                ["title"] = title,
                ["url"] = url,
                ["mode"] = "playwright-page",
                ["pageCount"] = browserContext.Pages.Count.ToString(),
                ["state"] = State.ToString(),
            });
    }

    [BrowserPluginAction("inspect-playwright", "读取 Playwright 对象", Description = "直接读取宿主传入的 IBrowserContext 和 IPage。")]
    public async Task<BrowserPluginActionResult> InspectPlaywrightAsync(IBrowserContext browserContext, IPage? activePage, CancellationToken cancellationToken)
    {
        if (State != BrowserPluginState.Running)
            return new BrowserPluginActionResult("请先启动插件，再执行导出函数。", BuildStateData());

        string? title = null;
        if (activePage is not null)
        {
            try
            {
                title = await activePage.TitleAsync();
            }
            catch
            {
            }
        }

        return new BrowserPluginActionResult(
            activePage is null ? "已拿到 BrowserContext，但当前没有活动页面。" : $"已直接拿到 Playwright ActivePage：{activePage.Url}",
            new Dictionary<string, string?>
            {
                ["state"] = State.ToString(),
                ["pageCount"] = browserContext.Pages.Count.ToString(),
                ["activePageUrl"] = activePage?.Url,
                ["activePageTitle"] = title,
            });
    }

    private IReadOnlyDictionary<string, string?> BuildStateData()
        => new Dictionary<string, string?>
        {
            ["state"] = State.ToString(),
        };
}

