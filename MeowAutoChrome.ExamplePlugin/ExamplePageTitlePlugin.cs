using MeowAutoChrome.Contracts;

namespace MeowAutoChrome.ExamplePlugin;

[BrowserPlugin("example.page-title", "Example 标题插件", Description = "示例插件：读取当前活动网页标题和地址。")]
public sealed class ExamplePageTitlePlugin : BrowserPluginBase
{
    public override bool SupportsPause => true;

    protected override string PluginName => "Example 插件";

    [BrowserPluginAction("读取网页标题", Description = "直接通过当前活动页面读取 document.title。")]
    public async Task<BrowserPluginActionResult> ReadTitleAsync(
        [BrowserPluginInput("结果前缀", Description = "可选，自定义返回消息前缀。")] string prefix = "当前页面标题"
        )
    {
        CurrentCancellationToken.ThrowIfCancellationRequested();

        if (State != BrowserPluginState.Running)
            return this.OkResult("请先启动插件，再执行导出函数。");

        if (CurrentActivePage is null)
            return this.OkResult("当前没有活动页面。");

        var page = RequireActivePage();
        var title = await page.EvaluateAsync<string?>("() => document?.title ?? null");
        var url = page.Url;

        return this.OkResult(
            string.IsNullOrWhiteSpace(title) ? "当前页面没有可用标题。" : $"{prefix}：{title}",
            new Dictionary<string, string?>
            {
                ["title"] = title,
                ["url"] = url,
                ["mode"] = "playwright-page",
                ["pageCount"] = CurrentBrowserContext.Pages.Count.ToString(),
            });
    }

    [BrowserPluginAction("读取 Playwright 对象", Description = "直接读取宿主注入的 BrowserContext 和 ActivePage。")]
    public async Task<BrowserPluginActionResult> InspectPlaywrightAsync()
    {
        CurrentCancellationToken.ThrowIfCancellationRequested();

        if (State != BrowserPluginState.Running)
            return this.OkResult("请先启动插件，再执行导出函数。");

        string? title = null;
        if (CurrentActivePage is not null)
        {
            try
            {
                title = await CurrentActivePage.TitleAsync();
            }
            catch
            {
            }
        }

        return this.OkResult(
            CurrentActivePage is null ? "已拿到 BrowserContext，但当前没有活动页面。" : $"已直接拿到 Playwright ActivePage：{CurrentActivePage.Url}",
            new Dictionary<string, string?>
            {
                ["pageCount"] = CurrentBrowserContext.Pages.Count.ToString(),
                ["activePageUrl"] = CurrentActivePage?.Url,
                ["activePageTitle"] = title,
            });
    }

    [BrowserPluginAction("读取指定 ID 元素", Description = "输入 DOM 元素 ID，读取该元素的文本内容。")]
    public async Task<BrowserPluginActionResult> ReadElementByIdAsync(
        [BrowserPluginInput("元素 ID", Description = "要读取的 DOM id。")] string elementId
        )
    {
        CurrentCancellationToken.ThrowIfCancellationRequested();

        if (State != BrowserPluginState.Running)
            return this.OkResult("请先启动插件，再执行导出函数。");

        if (CurrentActivePage is null)
            return this.OkResult("当前没有活动页面。");

        if (string.IsNullOrWhiteSpace(elementId))
            return this.OkResult("请提供 elementId。");

        var page = RequireActivePage();
        var normalizedElementId = elementId.Trim();
        var text = await page.EvaluateAsync<string?>(
            """
            (id) => {
                const element = document.getElementById(id);
                return element ? element.textContent?.trim() ?? null : null;
            }
            """,
            normalizedElementId);

        return this.OkResult(
            string.IsNullOrWhiteSpace(text) ? "未找到对应元素，或元素内容为空。" : $"元素内容：{text}",
            new Dictionary<string, string?>
            {
                ["elementId"] = normalizedElementId,
                ["text"] = text,
                ["url"] = page.Url,
                ["pageCount"] = CurrentBrowserContext.Pages.Count.ToString(),
            });
    }
}

