using MeowAutoChrome.Contracts;
using MeowAutoChrome.Web.Models;
using MeowAutoChrome.Web.Services;
using Microsoft.Playwright;
using System.Net;

namespace MeowAutoChrome.Web.Warpper;

public record BrowserTabInfo(string Id, string? Title, string? Url, bool IsSelected);

public class PlayWrightWarpper
{
    private readonly Dictionary<IPage, string> _pageIds = new(ReferenceEqualityComparer.Instance);
    private readonly ProgramSettingsService _programSettingsService;
    private int _viewportWidth = 1280;
    private int _viewportHeight = 800;

    public static IPlaywright Playwright { get; } = Microsoft.Playwright.Playwright.CreateAsync().GetAwaiter().GetResult();
    public string? LastErrorMessage { get; private set; }
    public string? SelectedPageId { get; private set; }
    public IBrowserContext BrowserContext { get; }
    public IReadOnlyList<IPage> Pages => BrowserContext.Pages;
    public IPage? ActivePage
    {
        get
        {
            CleanupTrackedPages();

            if (SelectedPageId != null)
            {
                var selected = Pages.FirstOrDefault(page => _pageIds.TryGetValue(page, out var id) && id == SelectedPageId);
                if (selected != null)
                    return selected;
            }
            var fallback = Pages.Count > 0 ? Pages[^1] : null;
            if (fallback != null) SelectedPageId = EnsurePageId(fallback);

            return fallback;
        }
    }
    private static bool IsHttpUri(Uri uri)
        => uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;

    private static bool LooksLikeHost(string input)
    {
        if (string.IsNullOrWhiteSpace(input) || input.Contains(' ', StringComparison.Ordinal))
            return false;

        var candidate = input;
        var slashIndex = candidate.IndexOf('/');
        if (slashIndex >= 0)
            candidate = candidate[..slashIndex];

        if (candidate.StartsWith("localhost", StringComparison.OrdinalIgnoreCase))
            return true;

        var portSeparatorIndex = candidate.LastIndexOf(':');
        if (portSeparatorIndex > 0 && candidate.Count(c => c == ':') == 1)
            candidate = candidate[..portSeparatorIndex];

        return candidate.Contains('.', StringComparison.Ordinal) || IPAddress.TryParse(candidate, out _);
    }

    private static bool TryBuildNavigationUri(string input, out Uri? uri)
    {
        uri = null;

        if (string.IsNullOrWhiteSpace(input))
            return false;

        input = input.Trim();

        if (Uri.TryCreate(input, UriKind.Absolute, out var absoluteUri) && IsHttpUri(absoluteUri) && !string.IsNullOrWhiteSpace(absoluteUri.Host))
        {
            uri = absoluteUri;
            return true;
        }

        if (input.StartsWith("http:", StringComparison.OrdinalIgnoreCase) && !input.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            input = $"http://{input[5..].TrimStart('/')}";
        else if (input.StartsWith("https:", StringComparison.OrdinalIgnoreCase) && !input.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            input = $"https://{input[6..].TrimStart('/')}";
        else if (LooksLikeHost(input))
            input = (input.StartsWith("localhost", StringComparison.OrdinalIgnoreCase) || IPAddress.TryParse(input.Split('/', ':')[0], out _)
                ? "http://"
                : "https://") + input;

        if (Uri.TryCreate(input, UriKind.Absolute, out absoluteUri) && IsHttpUri(absoluteUri) && !string.IsNullOrWhiteSpace(absoluteUri.Host))
        {
            uri = absoluteUri;
            return true;
        }

        return false;
    }

    private async Task<Uri> BuildSearchUriAsync(string query)
    {
        var settings = await _programSettingsService.GetAsync();
        var template = string.IsNullOrWhiteSpace(settings.SearchUrlTemplate)
            ? ProgramSettings.DefaultSearchUrlTemplate
            : settings.SearchUrlTemplate.Trim();

        var searchUrl = template.Replace("{query}", Uri.EscapeDataString(query.Trim()), StringComparison.OrdinalIgnoreCase);
        return new Uri(searchUrl, UriKind.Absolute);
    }

    public string? CurrentUrl => ActivePage?.Url;

    public PlayWrightWarpper(ProgramSettingsService programSettingsService, string? userDataDir = null)
    {
        _programSettingsService = programSettingsService;

        BrowserContext = Playwright.Chromium.LaunchPersistentContextAsync(
            userDataDir ?? Path.Combine(AppContext.BaseDirectory, "user_data"),
            new BrowserTypeLaunchPersistentContextOptions
            {
                Headless = true,
                Args = [
                    "--no-first-run",
                    "--disable-blink-features=AutomationControlled",
                ],
                ViewportSize = null
            }
        ).GetAwaiter().GetResult();
    }


    private string EnsurePageId(IPage page)
    {
        if (_pageIds.TryGetValue(page, out var existing)) return existing;
        var id = Guid.NewGuid().ToString("N");
        _pageIds[page] = id;
        return id;
    }

    private void CleanupTrackedPages()
    {
        var openPages = BrowserContext.Pages.ToHashSet(ReferenceEqualityComparer.Instance);
        var closedPages = _pageIds.Keys.Where(page => !openPages.Contains(page)).ToList();
        foreach (var page in closedPages) _pageIds.Remove(page);
        if (SelectedPageId != null && !_pageIds.ContainsValue(SelectedPageId)) SelectedPageId = null;
    }

    private async Task ApplyViewportSizeAsync(IPage page)
    {
        try
        {
            await page.SetViewportSizeAsync(_viewportWidth, _viewportHeight);
        }
        catch
        {
        }
    }

    public async Task<IReadOnlyList<BrowserTabInfo>> GetTabsAsync()
    {
        CleanupTrackedPages();
        var activePage = ActivePage;
        var tabs = new List<BrowserTabInfo>();
        foreach (var page in BrowserContext.Pages)
        {
            var id = EnsurePageId(page);
            string? title = null;
            try
            {
                title = await page.TitleAsync();
            }
            catch
            {
            }

            tabs.Add(new BrowserTabInfo(id, title, page.Url, activePage == page));
        }

        return tabs;
    }

    public async Task<bool> SelectPageAsync(string pageId)
    {
        CleanupTrackedPages();

        var page = BrowserContext.Pages.FirstOrDefault(current => EnsurePageId(current) == pageId);
        if (page == null)
            return false;

        SelectedPageId = pageId;

        try
        {
            await ApplyViewportSizeAsync(page);
            await page.BringToFrontAsync();
        }
        catch
        {
        }

        return true;
    }

    public async Task CreateTabAsync(string? url = null)
    {
        CleanupTrackedPages();
        var previousSelectedPageId = SelectedPageId;

        var page = await BrowserContext.NewPageAsync();
        var createdPageId = EnsurePageId(page);

        await ApplyViewportSizeAsync(page);

        if (!string.IsNullOrWhiteSpace(url))
            await page.GotoAsync(url);

        SelectedPageId = previousSelectedPageId ?? createdPageId;
    }

    public async Task<bool> CloseTabAsync(string pageId)
    {
        CleanupTrackedPages();

        var pages = BrowserContext.Pages.ToList();
        var index = pages.FindIndex(current => EnsurePageId(current) == pageId);
        if (index < 0)
            return false;

        var page = pages[index];
        var wasSelected = string.Equals(SelectedPageId, pageId, StringComparison.OrdinalIgnoreCase);
        var fallbackPage = wasSelected
            ? index > 0 ? pages[index - 1] : pages.Skip(1).FirstOrDefault()
            : null;
        var fallbackPageId = fallbackPage == null ? null : EnsurePageId(fallbackPage);

        try
        {
            await page.CloseAsync();
        }
        catch
        {
            return false;
        }

        _pageIds.Remove(page);

        if (!wasSelected)
        {
            CleanupTrackedPages();
            return true;
        }

        if (fallbackPageId == null)
        {
            SelectedPageId = null;
            return true;
        }

        return await SelectPageAsync(fallbackPageId);
    }

    public async Task<string?> GetTitleAsync()
    {
        var page = ActivePage;
        if (page == null)
            return null;

        try
        {
            return await page.TitleAsync();
        }
        catch (PlaywrightException)
        {
            return null;
        }
    }

    public async Task NavigateAsync(string url)
    {
        LastErrorMessage = null;

        if (string.IsNullOrWhiteSpace(url))
        {
            LastErrorMessage = "请输入地址。";
            return;
        }

        Uri uri;
        if (!TryBuildNavigationUri(url, out uri))
            uri = await BuildSearchUriAsync(url);

        var page = ActivePage ?? await BrowserContext.NewPageAsync();
        SelectedPageId = EnsurePageId(page);

        try
        {
            await ApplyViewportSizeAsync(page);
            await page.GotoAsync(uri.AbsoluteUri);
        }
        catch (PlaywrightException ex)
        {
            LastErrorMessage = $"导航失败：{ex.Message}";
        }
    }

    public async Task<bool> GoBackAsync()
    {
        var page = ActivePage;
        if (page == null) return false;
        return await page.GoBackAsync() != null;
    }

    public async Task<bool> GoForwardAsync()
    {
        var page = ActivePage;
        if (page == null) return false;
        return await page.GoForwardAsync() != null;
    }

    public async Task ReloadAsync()
    {
        var page = ActivePage;
        if (page == null) return;
        await page.ReloadAsync();
    }

    public async Task<byte[]?> CaptureScreenshotAsync()
    {
        var page = ActivePage;
        if (page == null) return null;

        return await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Type = ScreenshotType.Png,
            FullPage = false,
        });
    }

    public async Task SetViewportSizeAsync(int width, int height)
    {
        _viewportWidth = Math.Max(320, width);
        _viewportHeight = Math.Max(240, height);

        foreach (var page in BrowserContext.Pages.ToList())
            await ApplyViewportSizeAsync(page);
    }
}

