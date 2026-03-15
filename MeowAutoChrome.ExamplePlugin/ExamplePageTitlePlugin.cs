using MeowAutoChrome.Contracts;
using MeowAutoChrome.Contracts.Attributes;

namespace MeowAutoChrome.ExamplePlugin;

[BrowserPlugin("example.page-title", "Example 标题插件", Description = "示例插件：读取当前活动网页标题和地址。")]
public sealed class ExamplePageTitlePlugin : BrowserPluginBase
{
    public override bool SupportsPause => true;

    protected override string PluginName => "Example 插件";

    [BrowserPluginAction("读取网页标题", Description = "直接通过当前活动页面读取 document.title。")]
    public async Task<BrowserPluginActionResult> ReadTitleAsync()
    {
        CurrentCancellationToken.ThrowIfCancellationRequested();

        if (CurrentActivePage is null)
            return this.OkResult("当前没有活动页面。");

        var page = RequireActivePage();
        var title = await page.EvaluateAsync<string?>("() => document?.title ?? null");
        var url = page.Url;

        return this.OkResult(
            string.IsNullOrWhiteSpace(title) ? "当前页面没有可用标题。" : title,
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

    [BrowserPluginAction("generate-letter", "生成信件", Description = "按当前页面标题和地址生成一封示例信件，并实时推送进度。")]
    public async Task<BrowserPluginActionResult> GenerateLetterAsync(
        [BrowserPluginInput("收件人", Description = "留空时默认写作“朋友”。", Name = "recipient")] string? recipient = null)
    {
        CurrentCancellationToken.ThrowIfCancellationRequested();

        if (CurrentActivePage is null)
            return this.OkResult("当前没有活动页面，无法生成信件。");

        var page = RequireActivePage();
        var normalizedRecipient = string.IsNullOrWhiteSpace(recipient) ? "朋友" : recipient.Trim();

        await PublishUpdateAsync("正在读取页面标题…", new Dictionary<string, string?>
        {
            ["step"] = "1/3",
            ["recipient"] = normalizedRecipient,
        });
        await Task.Delay(150, CurrentCancellationToken);

        var title = await page.EvaluateAsync<string?>("() => document?.title ?? null");
        var normalizedTitle = string.IsNullOrWhiteSpace(title) ? "未命名页面" : title.Trim();

        await PublishUpdateAsync("正在整理页面地址…", new Dictionary<string, string?>
        {
            ["step"] = "2/3",
            ["title"] = normalizedTitle,
            ["url"] = page.Url,
        });
        await Task.Delay(150, CurrentCancellationToken);

        var letter = $"亲爱的{normalizedRecipient}：\n\n我刚刚浏览了一个页面《{normalizedTitle}》。\n如果你也想看看，可以打开：{page.Url}\n\n祝好。";

        await PublishUpdateAsync("正在生成信件正文…", new Dictionary<string, string?>
        {
            ["step"] = "3/3",
            ["letter"] = letter,
        });

        return this.OkResult("信件已生成。", new Dictionary<string, string?>
        {
            ["recipient"] = normalizedRecipient,
            ["title"] = normalizedTitle,
            ["url"] = page.Url,
            ["letter"] = letter,
        });
    }
}

