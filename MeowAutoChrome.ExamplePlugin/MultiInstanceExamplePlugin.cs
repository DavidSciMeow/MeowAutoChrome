using MeowAutoChrome.Contracts;
using MeowAutoChrome.Contracts.Attributes;

namespace MeowAutoChrome.ExamplePlugin;

[BrowserPlugin("example.multi-instance", "Example 多实例插件", Description = "示例插件：创建独立的 Playwright Chromium 实例，并让前端 TAB 按实例分组显示。")]
public sealed class MultiInstanceExamplePlugin : BrowserPluginBase
{
    private const string PluginId = "example.multi-instance";

    protected override string PluginName => "Example 多实例插件";

    [BrowserPluginAction("create-instance", "创建独立实例", Description = "创建一个新的独立 Chromium 实例，可选打开初始化地址。")]
    public async Task<BrowserPluginActionResult> CreateInstanceAsync(
        [BrowserPluginInput("实例名称", Description = "留空时自动生成。", Name = "displayName")] string? displayName = null,
        [BrowserPluginInput("初始化地址", Description = "留空时默认打开 about:blank。", Name = "url")] string? url = null)
    {
        CurrentCancellationToken.ThrowIfCancellationRequested();

        var instanceId = await CurrentBrowserInstanceManager.CreateBrowserInstanceAsync(PluginId, displayName, CurrentCancellationToken);
        var color = CurrentBrowserInstanceManager.GetInstanceColor(instanceId) ?? "#2563eb";
        var context = CurrentBrowserInstanceManager.GetBrowserContext(instanceId);
        if (context is not null)
        {
            var page = await context.NewPageAsync();
            if (!string.IsNullOrWhiteSpace(url))
                await page.GotoAsync(url.Trim());
        }

        await CurrentBrowserInstanceManager.SelectBrowserInstanceAsync(instanceId, CurrentCancellationToken);

        return this.OkResult(
            $"已创建独立实例：{instanceId}",
            new Dictionary<string, string?>
            {
                ["instanceId"] = instanceId,
                ["color"] = color,
                ["displayName"] = displayName,
                ["currentInstanceId"] = CurrentBrowserInstanceId,
            });
    }

    [BrowserPluginAction("list-instances", "列出实例", Description = "列出本插件创建的全部浏览器实例及颜色。")]
    public Task<BrowserPluginActionResult> ListInstancesAsync()
    {
        var instanceIds = CurrentBrowserInstanceManager.GetPluginInstanceIds(PluginId);
        var data = new Dictionary<string, string?>
        {
            ["count"] = instanceIds.Count.ToString()
        };

        for (var index = 0; index < instanceIds.Count; index++)
        {
            var instanceId = instanceIds[index];
            data[$"instance_{index}"] = $"{instanceId} | {CurrentBrowserInstanceManager.GetInstanceColor(instanceId)}";
        }

        return Task.FromResult(this.OkResult($"当前共有 {instanceIds.Count} 个独立实例。", data));
    }

    [BrowserPluginAction("navigate-all", "全部实例导航", Description = "让本插件创建的所有实例同时跳转到指定地址。")]
    public async Task<BrowserPluginActionResult> NavigateAllInstancesAsync(
        [BrowserPluginInput("目标地址", Description = "例如 https://example.com", Name = "url", Required = true)] string url)
    {
        CurrentCancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(url))
            return this.OkResult("请提供目标地址。");

        var normalizedUrl = url.Trim();
        var instanceIds = CurrentBrowserInstanceManager.GetPluginInstanceIds(PluginId);
        var navigatedCount = 0;

        foreach (var instanceId in instanceIds)
        {
            CurrentCancellationToken.ThrowIfCancellationRequested();

            var page = CurrentBrowserInstanceManager.GetActivePage(instanceId);
            if (page is null)
                continue;

            await page.GotoAsync(normalizedUrl);
            navigatedCount++;
        }

        return this.OkResult(
            $"已让 {navigatedCount} 个实例跳转到 {normalizedUrl}。",
            new Dictionary<string, string?>
            {
                ["url"] = normalizedUrl,
                ["navigatedCount"] = navigatedCount.ToString(),
            });
    }

    [BrowserPluginAction("remove-instance", "移除实例", Description = "关闭并移除一个指定实例。")]
    public async Task<BrowserPluginActionResult> RemoveInstanceAsync(
        [BrowserPluginInput("实例 ID", Description = "可先调用“列出实例”获取。", Name = "instanceId", Required = true)] string instanceId)
    {
        CurrentCancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(instanceId))
            return this.OkResult("请提供实例 ID。");

        var normalizedInstanceId = instanceId.Trim();
        var removed = await CurrentBrowserInstanceManager.RemoveBrowserInstanceAsync(normalizedInstanceId, CurrentCancellationToken);
        return this.OkResult(removed ? $"已移除实例：{normalizedInstanceId}" : $"未找到实例：{normalizedInstanceId}");
    }

    public override async Task<BrowserPluginActionResult> StopAsync()
    {
        foreach (var instanceId in CurrentBrowserInstanceManager.GetPluginInstanceIds(PluginId))
        {
            try
            {
                await CurrentBrowserInstanceManager.RemoveBrowserInstanceAsync(instanceId, CurrentCancellationToken);
            }
            catch
            {
            }
        }

        return await base.StopAsync();
    }
}
