using MeowAutoChrome.Web.Models;
using MeowAutoChrome.Web.Services;
using Microsoft.Playwright;
using System.Net;

namespace MeowAutoChrome.Web.Warpper;

public record BrowserTabInfo(string Id, string? Title, string? Url, bool IsSelected);

public class PlayWrightWarpper
{
    private readonly Dictionary<IPage, string> _pageIds = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<IPage, BrowserPageDiagnostics> _pageDiagnostics = new(ReferenceEqualityComparer.Instance);
    private readonly SemaphoreSlim _launchSettingsSemaphore = new(1, 1);
    private readonly Lock _syncRoot = new();
    private readonly ILogger<PlayWrightWarpper> _logger;
    private readonly ProgramSettingsService _programSettingsService;
    private int _viewportWidth = 1280;
    private int _viewportHeight = 800;

    public static IPlaywright Playwright { get; } = Microsoft.Playwright.Playwright.CreateAsync().GetAwaiter().GetResult();
    public string? LastErrorMessage { get; private set; }
    public string? SelectedPageId { get; private set; }
    public string UserDataDirectoryPath { get; private set; } = string.Empty;
    public bool IsHeadless { get; private set; }
    public IBrowserContext BrowserContext { get; private set; }
    public IReadOnlyList<IPage> Pages => BrowserContext.Pages;
    public string? CurrentUrl => ActivePage?.Url;
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


    public PlayWrightWarpper(ProgramSettingsService programSettingsService, ILogger<PlayWrightWarpper> logger, string? userDataDir = null)
    {
        _programSettingsService = programSettingsService;
        _logger = logger;

        var settings = _programSettingsService.GetAsync().GetAwaiter().GetResult();
        var configuredUserDataDir = string.IsNullOrWhiteSpace(userDataDir)
            ? settings.UserDataDirectory
            : Path.GetFullPath(userDataDir);

        PrepareUserDataDirectory(configuredUserDataDir, ProgramSettings.GetLegacyUserDataDirectoryPath());
        ActivateBrowserContext(CreateBrowserContextAsync(configuredUserDataDir, settings.Headless).GetAwaiter().GetResult(), configuredUserDataDir, settings.Headless);
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

        var closedDiagnostics = _pageDiagnostics.Keys.Where(page => !openPages.Contains(page)).ToList();
        foreach (var page in closedDiagnostics) _pageDiagnostics.Remove(page);

        if (SelectedPageId != null && !_pageIds.ContainsValue(SelectedPageId)) SelectedPageId = null;
    }

    private void RegisterPage(IPage page)
    {
        var shouldAttachHandlers = false;

        lock (_syncRoot)
        {
            EnsurePageId(page);

            if (!_pageDiagnostics.ContainsKey(page))
            {
                _pageDiagnostics[page] = new BrowserPageDiagnostics();
                shouldAttachHandlers = true;
            }
        }

        if (!shouldAttachHandlers)
            return;

        page.RequestFailed += (_, request) => RecordRequestFailure(page, request);
        page.PageError += (_, message) => RecordPageError(page, message);
        page.FrameNavigated += (_, frame) => HandleFrameNavigated(page, frame);
        page.Close += (_, _) => CleanupTrackedPages();
    }

    private void HandleFrameNavigated(IPage page, IFrame frame)
    {
        if (frame.ParentFrame is not null)
            return;

        BrowserPageDiagnosticsSnapshot snapshot;
        var currentUrl = frame.Url;

        lock (_syncRoot)
        {
            var diagnostics = GetOrCreateDiagnostics(page);
            diagnostics.PreviousMainFrameUrl = diagnostics.CurrentMainFrameUrl;
            diagnostics.CurrentMainFrameUrl = currentUrl;

            if (!IsChromeErrorPage(currentUrl))
            {
                diagnostics.ChromeErrorLogged = false;
                diagnostics.LastChromeErrorUrl = null;
                return;
            }

            if (diagnostics.ChromeErrorLogged && string.Equals(diagnostics.LastChromeErrorUrl, currentUrl, StringComparison.OrdinalIgnoreCase))
                return;

            diagnostics.ChromeErrorLogged = true;
            diagnostics.LastChromeErrorUrl = currentUrl;
            snapshot = diagnostics.ToSnapshot();
        }

        _logger.LogError(
            "检测到浏览器错误页。PageId: {PageId}; CurrentUrl: {CurrentUrl}; PreviousUrl: {PreviousUrl}; LastRequestFailure: {LastRequestFailure}; LastPageError: {LastPageError}",
            EnsurePageId(page),
            currentUrl,
            snapshot.PreviousMainFrameUrl ?? "(unknown)",
            snapshot.LastRequestFailure ?? "(none)",
            snapshot.LastPageError ?? "(none)");
    }

    private void RecordRequestFailure(IPage page, IRequest request)
    {
        lock (_syncRoot)
        {
            var diagnostics = GetOrCreateDiagnostics(page);
            var failureText = request.Failure ?? "未知失败";
            diagnostics.AddRequestFailure($"{request.Method} {request.Url} => {failureText}");
        }
    }

    private void RecordPageError(IPage page, string message)
    {
        lock (_syncRoot)
        {
            var diagnostics = GetOrCreateDiagnostics(page);
            diagnostics.LastPageError = string.IsNullOrWhiteSpace(message) ? null : message.Trim();
        }
    }

    private BrowserPageDiagnostics GetOrCreateDiagnostics(IPage page)
    {
        if (_pageDiagnostics.TryGetValue(page, out var diagnostics))
            return diagnostics;

        diagnostics = new BrowserPageDiagnostics();
        _pageDiagnostics[page] = diagnostics;
        return diagnostics;
    }

    private static bool IsChromeErrorPage(string? url)
        => !string.IsNullOrWhiteSpace(url)
            && url.StartsWith("chrome-error://chromewebdata/", StringComparison.OrdinalIgnoreCase);

    private async Task<IBrowserContext> CreateBrowserContextAsync(string userDataDir, bool isHeadless)
    {
        Directory.CreateDirectory(userDataDir);
        return await Playwright.Chromium.LaunchPersistentContextAsync(
            userDataDir,
            new BrowserTypeLaunchPersistentContextOptions
            {
                Headless = isHeadless,
                Args = [
                    "--no-first-run",
                    "--disable-blink-features=AutomationControlled",
                ],
                ViewportSize = null
            });
    }

    private void RegisterBrowserContext(IBrowserContext browserContext)
    {
        foreach (var page in browserContext.Pages)
            RegisterPage(page);

        browserContext.Page += (_, page) => RegisterPage(page);
    }

    private void ActivateBrowserContext(IBrowserContext browserContext, string userDataDir, bool isHeadless)
    {
        BrowserContext = browserContext;
        UserDataDirectoryPath = userDataDir;
        IsHeadless = isHeadless;

        lock (_syncRoot)
        {
            _pageIds.Clear();
            _pageDiagnostics.Clear();
            SelectedPageId = null;
        }

        RegisterBrowserContext(browserContext);
    }

    public async Task UpdateUserDataDirectoryAsync(string userDataDir)
        => await UpdateLaunchSettingsAsync(userDataDir, IsHeadless);

    public async Task UpdateLaunchSettingsAsync(string userDataDir, bool isHeadless)
    {
        var targetDirectory = Path.GetFullPath(userDataDir);

        await _launchSettingsSemaphore.WaitAsync();
        try
        {
            if (string.Equals(UserDataDirectoryPath, targetDirectory, StringComparison.OrdinalIgnoreCase) && IsHeadless == isHeadless)
                return;

            var previousContext = BrowserContext;
            var previousUserDataDirectory = UserDataDirectoryPath;
            var previousIsHeadless = IsHeadless;
            var directoryChanged = !string.Equals(previousUserDataDirectory, targetDirectory, StringComparison.OrdinalIgnoreCase);
            IBrowserContext? nextContext = null;

            LastErrorMessage = null;

            try
            {
                if (previousContext != null)
                    await previousContext.CloseAsync();

                PrepareUserDataDirectory(targetDirectory, previousUserDataDirectory);
                nextContext = await CreateBrowserContextAsync(targetDirectory, isHeadless);
                ActivateBrowserContext(nextContext, targetDirectory, isHeadless);

                _logger.LogInformation("浏览器启动设置已更新。UserDataDirectoryPath: {UserDataDirectoryPath}; Headless: {Headless}", targetDirectory, isHeadless);
            }
            catch (Exception ex)
            {
                if (nextContext is not null)
                {
                    try
                    {
                        await nextContext.CloseAsync();
                    }
                    catch
                    {
                    }
                }

                _logger.LogError(ex, "浏览器启动设置更新失败。UserDataDirectoryPath: {UserDataDirectoryPath}; Headless: {Headless}", targetDirectory, isHeadless);

                try
                {
                    if (directoryChanged)
                        PrepareUserDataDirectory(previousUserDataDirectory, targetDirectory);

                    if (previousContext != null)
                    {
                        var restoredContext = await CreateBrowserContextAsync(previousUserDataDirectory, previousIsHeadless);
                        ActivateBrowserContext(restoredContext, previousUserDataDirectory, previousIsHeadless);
                        _logger.LogWarning("浏览器启动设置回滚成功。UserDataDirectoryPath: {UserDataDirectoryPath}; Headless: {Headless}", previousUserDataDirectory, previousIsHeadless);
                    }
                }
                catch (Exception restoreException)
                {
                    _logger.LogCritical(restoreException, "浏览器启动设置回滚失败。OriginalUserDataDirectoryPath: {UserDataDirectoryPath}; OriginalHeadless: {Headless}", previousUserDataDirectory, previousIsHeadless);
                }

                LastErrorMessage = $"浏览器启动设置更新失败：{ex.Message}";
                throw new InvalidOperationException(LastErrorMessage, ex);
            }
        }
        finally
        {
            _launchSettingsSemaphore.Release();
        }
    }

    private void PrepareUserDataDirectory(string targetDirectory, string? sourceDirectory)
    {
        Directory.CreateDirectory(targetDirectory);

        if (string.IsNullOrWhiteSpace(sourceDirectory))
            return;

        var normalizedSourceDirectory = Path.GetFullPath(sourceDirectory);
        if (string.Equals(normalizedSourceDirectory, targetDirectory, StringComparison.OrdinalIgnoreCase))
            return;

        if (!Directory.Exists(normalizedSourceDirectory) || !Directory.EnumerateFileSystemEntries(normalizedSourceDirectory).Any())
            return;

        if (IsNestedDirectory(normalizedSourceDirectory, targetDirectory) || IsNestedDirectory(targetDirectory, normalizedSourceDirectory))
            throw new InvalidOperationException("浏览器用户数据目录不能设置为当前目录的子目录或父目录。");

        MoveDirectoryContents(normalizedSourceDirectory, targetDirectory);
        _logger.LogInformation("浏览器用户数据已从 {SourceDirectory} 迁移到 {TargetDirectory}", normalizedSourceDirectory, targetDirectory);
    }

    private static bool IsNestedDirectory(string parentPath, string childPath)
    {
        var normalizedParentPath = Path.TrimEndingDirectorySeparator(parentPath);
        var normalizedChildPath = Path.TrimEndingDirectorySeparator(childPath);
        return normalizedChildPath.StartsWith(normalizedParentPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || normalizedChildPath.StartsWith(normalizedParentPath + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static void MoveDirectoryContents(string sourceDirectory, string targetDirectory)
    {
        if (!Directory.Exists(targetDirectory))
        {
            Directory.Move(sourceDirectory, targetDirectory);
            return;
        }

        foreach (var directoryPath in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, directoryPath);
            Directory.CreateDirectory(Path.Combine(targetDirectory, relativePath));
        }

        foreach (var filePath in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, filePath);
            var targetFilePath = Path.Combine(targetDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(targetFilePath)!);
            File.Move(filePath, targetFilePath, overwrite: true);
        }

        foreach (var directoryPath in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories).OrderByDescending(path => path.Length))
        {
            if (!Directory.EnumerateFileSystemEntries(directoryPath).Any())
                Directory.Delete(directoryPath, recursive: false);
        }

        if (!Directory.EnumerateFileSystemEntries(sourceDirectory).Any())
            Directory.Delete(sourceDirectory, recursive: false);
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

        if (!TryBuildNavigationUri(url, out var uri))
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

    private sealed class BrowserPageDiagnostics
    {
        private const int MaxRequestFailures = 5;
        private readonly Queue<string> _recentRequestFailures = new();

        public string? PreviousMainFrameUrl { get; set; }
        public string? CurrentMainFrameUrl { get; set; }
        public string? LastChromeErrorUrl { get; set; }
        public string? LastPageError { get; set; }
        public bool ChromeErrorLogged { get; set; }

        public void AddRequestFailure(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            _recentRequestFailures.Enqueue(message);
            while (_recentRequestFailures.Count > MaxRequestFailures)
                _recentRequestFailures.Dequeue();
        }

        public BrowserPageDiagnosticsSnapshot ToSnapshot()
            => new(PreviousMainFrameUrl, _recentRequestFailures.LastOrDefault(), LastPageError);
    }

    private sealed record BrowserPageDiagnosticsSnapshot(string? PreviousMainFrameUrl, string? LastRequestFailure, string? LastPageError);
}

