using MeowAutoChrome.Web.Models;
using MeowAutoChrome.Web.ProgarmControl;
using MeowAutoChrome.Web.Services;
using Microsoft.Playwright;
using System.Net;

namespace MeowAutoChrome.Web.Warpper;

/// <summary>
/// 表示浏览器选项卡（Tab / Page）的视图信息，用于前端列出标签页。
/// </summary>
/// <param name="Id">页面唯一标识符。</param>
/// <param name="Title">页面标题（若可用）。</param>
/// <param name="Url">页面当前 URL。</param>
/// <param name="IsSelected">是否为当前选中的页面。</param>
/// <param name="InstanceId">所属浏览器实例 ID。</param>
/// <param name="InstanceName">所属浏览器实例显示名称。</param>
/// <param name="InstanceColor">所属实例颜色（用于 UI 突出）。</param>
/// <param name="InstanceOwnerId">所属插件 ID（如果是插件创建的实例）。</param>
/// <param name="IsInSelectedInstance">是否位于当前选中实例中。</param>
public record BrowserTabInfo(
    string Id,
    string? Title,
    string? Url,
    bool IsSelected,
    string InstanceId,
    string InstanceName,
    string InstanceColor,
    string? InstanceOwnerId,
    bool IsInSelectedInstance);

/// <summary>
/// PlayWrightWarpper 封装 Playwright 的浏览器上下文与页面管理，负责启动/关闭浏览器上下文、管理标签页、导航与截图等操作。
/// 该类在项目中以实例表示一个可单独配置的浏览器实例（例如不同的 user-data-dir、User-Agent、headless 模式等）。
/// </summary>
public class PlayWrightWarpper
{
    private readonly Dictionary<IPage, string> _pageIds = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<IPage, BrowserPageDiagnostics> _pageDiagnostics = new(ReferenceEqualityComparer.Instance);
    private readonly SemaphoreSlim _launchSettingsSemaphore = new(1, 1);
    private readonly Lock _syncRoot = new();
    private readonly ILogger<PlayWrightWarpper> _logger;
    private readonly ProgramSettingsService _programSettingsService;
    private int _configuredViewportWidth = 1280;
    private int _configuredViewportHeight = 800;
    private int _viewportWidth = 1280;
    private int _viewportHeight = 800;
    private bool _autoResizeViewport;
    private bool _preserveAspectRatio;
    private bool _useProgramUserAgent = true;
    private string? _customUserAgent;

    /// <summary>
    /// 全局 Playwright 实例。
    /// </summary>
    public static IPlaywright Playwright { get; } = Microsoft.Playwright.Playwright.CreateAsync().GetAwaiter().GetResult();
    /// <summary>
    /// 此浏览器实例的唯一标识符。
    /// </summary>
    public string InstanceId { get; }
    /// <summary>
    /// 供 UI 显示的实例名称。
    /// </summary>
    public string DisplayName { get; }
    /// <summary>
    /// 实例用于 UI 的颜色字符串（例如 CSS color）。
    /// </summary>
    public string Color { get; }
    /// <summary>
    /// 如果此实例由插件创建，则为插件 ID，否则为 null。
    /// </summary>
    public string? OwnerPluginId { get; }
    /// <summary>
    /// 最近一次发生的错误信息（用于界面展示），仅供诊断使用。
    /// </summary>
    public string? LastErrorMessage { get; private set; }
    /// <summary>
    /// 当前选中的页面 ID（由 EnsurePageId 生成并管理）。
    /// </summary>
    public string? SelectedPageId { get; private set; }
    /// <summary>
    /// 与此实例关联的用户数据目录路径。
    /// </summary>
    public string UserDataDirectoryPath { get; private set; } = string.Empty;
    /// <summary>
    /// 当前实例是否以 headless 模式运行。
    /// </summary>
    public bool IsHeadless { get; private set; }
    /// <summary>
    /// 用户配置的视口宽度。
    /// </summary>
    public int ConfiguredViewportWidth => _configuredViewportWidth;
    /// <summary>
    /// 用户配置的视口高度。
    /// </summary>
    public int ConfiguredViewportHeight => _configuredViewportHeight;
    /// <summary>
    /// 是否自动根据显示尺寸调整视口。
    /// </summary>
    public bool AutoResizeViewport => _autoResizeViewport;
    /// <summary>
    /// 是否保持原始宽高比以适配显示尺寸。
    /// </summary>
    public bool PreserveAspectRatio => _preserveAspectRatio;
    /// <summary>
    /// 是否使用程序级的 User-Agent（而非实例自定义的）。
    /// </summary>
    public bool UseProgramUserAgent => _useProgramUserAgent;
    /// <summary>
    /// 实例自定义的 User-Agent（当不使用程序级 User-Agent 时生效）。
    /// </summary>
    public string? CustomUserAgent => _customUserAgent;
    /// <summary>
    /// 当前的 Playwright 浏览器上下文（对应一个 profile / user-data-dir）。
    /// </summary>
    public IBrowserContext BrowserContext { get; private set; } = null!;
    /// <summary>
    /// 当前上下文中所有已打开的页面集合。
    /// </summary>
    public IReadOnlyList<IPage> Pages => BrowserContext.Pages;
    /// <summary>
    /// 当前选中页面的 URL（如果存在活动页面）。
    /// </summary>
    public string? CurrentUrl => ActivePage?.Url;
    /// <summary>
    /// 当前活动页面。
    /// </summary>
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


    /// <summary>
    /// 初始化浏览器实例包装器。
    /// </summary>
    /// <param name="programSettingsService">程序设置服务。</param>
    /// <param name="logger">日志记录器。</param>
    /// <param name="instanceId">实例 ID。</param>
    /// <param name="displayName">实例显示名称。</param>
    /// <param name="color">实例颜色。</param>
    /// <param name="ownerPluginId">所属插件 ID。</param>
    /// <param name="userDataDir">用户数据目录。</param>
    public PlayWrightWarpper(
        ProgramSettingsService programSettingsService,
        ILogger<PlayWrightWarpper> logger,
        string instanceId,
        string displayName,
        string color,
        string? ownerPluginId = null,
        string? userDataDir = null)
    {
        // 构造函数：初始化实例字段、读取程序设置并创建或激活对应的浏览器上下文。
        _programSettingsService = programSettingsService;
        _logger = logger;
        InstanceId = instanceId;
        DisplayName = displayName;
        Color = color;
        OwnerPluginId = ownerPluginId;

        var settings = _programSettingsService.GetAsync().GetAwaiter().GetResult();
        var configuredUserDataDir = string.IsNullOrWhiteSpace(userDataDir)
            ? settings.UserDataDirectory
            : Path.GetFullPath(userDataDir);

        PrepareUserDataDirectory(configuredUserDataDir, ProgramSettings.GetLegacyUserDataDirectoryPath());
        ActivateBrowserContext(CreateBrowserContextAsync(configuredUserDataDir, settings.Headless, ResolveEffectiveUserAgent(settings)).GetAwaiter().GetResult(), configuredUserDataDir, settings.Headless);
    }

    private static string? NormalizeOptionalValue(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private string? ResolveEffectiveUserAgent(ProgramSettings settings)
    {
        var programUserAgent = NormalizeOptionalValue(settings.UserAgent);
        if (!string.IsNullOrWhiteSpace(programUserAgent))
        {
            if (settings.AllowInstanceUserAgentOverride && !_useProgramUserAgent && !string.IsNullOrWhiteSpace(_customUserAgent))
                return _customUserAgent;

            return programUserAgent;
        }

        return !_useProgramUserAgent && !string.IsNullOrWhiteSpace(_customUserAgent)
            ? _customUserAgent
            : null;
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

    /// <summary>
    /// 判断当前实例是否包含指定标签页。
    /// </summary>
    /// <param name="pageId">标签页 ID。</param>
    /// <returns>是否包含该标签页。</returns>
    public bool ContainsTab(string pageId)
    {
        CleanupTrackedPages();
        return Pages.Any(page => EnsurePageId(page) == pageId);
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

    private static async Task<IBrowserContext> CreateBrowserContextAsync(string userDataDir, bool isHeadless, string? userAgent)
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
                UserAgent = userAgent,
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

    /// <summary>
    /// 关闭当前浏览器上下文并释放资源（如果存在）。
    /// </summary>
    public async Task CloseAsync()
    {
        if (BrowserContext is null)
            return;

        try
        {
            await BrowserContext.CloseAsync();
        }
        catch
        {
        }
    }

    /// <summary>
    /// 更新实例使用的用户数据目录（等同于更新启动设置中的 user-data-dir）。
    /// </summary>
    /// <param name="userDataDir">目标用户数据目录路径。</param>
    public async Task UpdateUserDataDirectoryAsync(string userDataDir)
        => await UpdateLaunchSettingsAsync(userDataDir, IsHeadless);

    /// <summary>
    /// 获取当前实例的视口设置（配置宽高与自动调整/保持宽高比选项）。
    /// </summary>
    /// <returns>视口设置响应对象。</returns>
    public BrowserInstanceViewportSettingsResponse GetViewportSettings()
        => new(_configuredViewportWidth, _configuredViewportHeight, _autoResizeViewport, _preserveAspectRatio);

    /// <summary>
    /// 更新实例级别的设置（包括 user-data-dir、headless、视口与 UA 等），并在需要时重新启动上下文。
    /// </summary>
    /// <param name="userDataDir">目标用户数据目录。</param>
    /// <param name="isHeadless">是否以无头模式运行。</param>
    /// <param name="viewportWidth">目标视口宽度。</param>
    /// <param name="viewportHeight">目标视口高度。</param>
    /// <param name="autoResizeViewport">是否自动调整视口。</param>
    /// <param name="preserveAspectRatio">是否保持宽高比。</param>
    /// <param name="useProgramUserAgent">是否使用程序级 User-Agent。</param>
    /// <param name="userAgent">实例自定义 User-Agent。</param>
    /// <param name="migrateUserData">是否迁移用户数据。</param>
    /// <param name="displayWidth">可选显示宽度。</param>
    /// <param name="displayHeight">可选显示高度。</param>
    public async Task UpdateInstanceSettingsAsync(string userDataDir, bool isHeadless, int viewportWidth, int viewportHeight, bool autoResizeViewport, bool preserveAspectRatio, bool useProgramUserAgent, string? userAgent, bool migrateUserData, int? displayWidth = null, int? displayHeight = null)
    {
        _configuredViewportWidth = Math.Max(320, viewportWidth);
        _configuredViewportHeight = Math.Max(240, viewportHeight);
        _autoResizeViewport = autoResizeViewport;
        _preserveAspectRatio = preserveAspectRatio;
        _useProgramUserAgent = useProgramUserAgent;
        _customUserAgent = NormalizeOptionalValue(userAgent);

        await UpdateLaunchSettingsAsync(userDataDir, isHeadless, migrateUserData);

        if (_autoResizeViewport && displayWidth is > 0 && displayHeight is > 0)
        {
            await ApplyDisplayViewportSizeAsync(displayWidth.Value, displayHeight.Value);
            return;
        }

        await ApplyConfiguredViewportSizeAsync();
    }

    /// <summary>
    /// 更新启动设置并在必要时重建或切换浏览器上下文。
    /// </summary>
    /// <param name="userDataDir">目标用户数据目录。</param>
    /// <param name="isHeadless">是否以无头模式运行。</param>
    /// <param name="migrateUserData">是否迁移用户数据。</param>
    /// <param name="forceReload">是否强制重建上下文。</param>
    public async Task UpdateLaunchSettingsAsync(string userDataDir, bool isHeadless, bool migrateUserData = true, bool forceReload = false)
    {
        var targetDirectory = Path.GetFullPath(userDataDir);

        await _launchSettingsSemaphore.WaitAsync();
        try
        {
            if (!forceReload && string.Equals(UserDataDirectoryPath, targetDirectory, StringComparison.OrdinalIgnoreCase) && IsHeadless == isHeadless)
                return;

            var previousContext = BrowserContext;
            var previousUserDataDirectory = UserDataDirectoryPath;
            var previousIsHeadless = IsHeadless;
            var directoryChanged = !string.Equals(previousUserDataDirectory, targetDirectory, StringComparison.OrdinalIgnoreCase);
            var settings = await _programSettingsService.GetAsync();
            var effectiveUserAgent = ResolveEffectiveUserAgent(settings);
            IBrowserContext? nextContext = null;

            LastErrorMessage = null;

            try
            {
                if (previousContext != null)
                    await previousContext.CloseAsync();

                PrepareUserDataDirectory(targetDirectory, previousUserDataDirectory, migrateUserData);
                nextContext = await CreateBrowserContextAsync(targetDirectory, isHeadless, effectiveUserAgent);
                ActivateBrowserContext(nextContext, targetDirectory, isHeadless);
                await ApplyConfiguredViewportSizeAsync();

                if(_logger.IsEnabled(LogLevel.Information))
                    _logger.LogInformation("浏览器启动设置已更新。UserDataDirectoryPath: {UserDataDirectoryPath}; Headless: {Headless}; UserAgent: {UserAgent}", targetDirectory, isHeadless, effectiveUserAgent ?? "(default)");
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
                    if (directoryChanged && migrateUserData)
                        PrepareUserDataDirectory(previousUserDataDirectory, targetDirectory, moveContents: true);

                    if (previousContext != null)
                    {
                        var restoredContext = await CreateBrowserContextAsync(previousUserDataDirectory, previousIsHeadless, effectiveUserAgent);
                        ActivateBrowserContext(restoredContext, previousUserDataDirectory, previousIsHeadless);
                        await ApplyConfiguredViewportSizeAsync();
                        if(_logger.IsEnabled(LogLevel.Warning))
                        _logger.LogWarning("浏览器启动设置回滚成功。UserDataDirectoryPath: {UserDataDirectoryPath}; Headless: {Headless}", previousUserDataDirectory, previousIsHeadless);
                    }
                }
                catch (Exception restoreException)
                {
                    if (_logger.IsEnabled(LogLevel.Critical))
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

    private void PrepareUserDataDirectory(string targetDirectory, string? sourceDirectory, bool moveContents = true)
    {
        Directory.CreateDirectory(targetDirectory);

        if (!moveContents || string.IsNullOrWhiteSpace(sourceDirectory))
            return;

        var normalizedSourceDirectory = Path.GetFullPath(sourceDirectory);
        if (string.Equals(normalizedSourceDirectory, targetDirectory, StringComparison.OrdinalIgnoreCase))
            return;

        if (!Directory.Exists(normalizedSourceDirectory) || !Directory.EnumerateFileSystemEntries(normalizedSourceDirectory).Any())
            return;

        if (IsNestedDirectory(normalizedSourceDirectory, targetDirectory) || IsNestedDirectory(targetDirectory, normalizedSourceDirectory))
            throw new InvalidOperationException("浏览器用户数据目录不能设置为当前目录的子目录或父目录。");

        MoveDirectoryContents(normalizedSourceDirectory, targetDirectory);
        if(_logger.IsEnabled(LogLevel.Information))
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

    private async Task ApplyViewportSizeInternalAsync(int width, int height)
    {
        _viewportWidth = Math.Max(320, width);
        _viewportHeight = Math.Max(240, height);

        foreach (var page in BrowserContext.Pages.ToList())
            await ApplyViewportSizeAsync(page);
    }

    /// <summary>
    /// 应用用户配置的视口尺寸到所有页面。若启用自动调整则此调用被忽略。
    /// </summary>
    public Task ApplyConfiguredViewportSizeAsync()
        => ApplyViewportSizeInternalAsync(_configuredViewportWidth, _configuredViewportHeight);

    /// <summary>
    /// 根据显示区域大小计算并应用给定的显示视口尺寸（考虑保持宽高比与自动调整设置）。
    /// </summary>
    /// <param name="displayWidth">显示宽度（像素）。</param>
    /// <param name="displayHeight">显示高度（像素）。</param>
    public async Task ApplyDisplayViewportSizeAsync(int displayWidth, int displayHeight)
    {
        if (!_autoResizeViewport)
        {
            await ApplyConfiguredViewportSizeAsync();
            return;
        }

        var targetWidth = Math.Max(320, displayWidth);
        var targetHeight = Math.Max(240, displayHeight);

        if (_preserveAspectRatio)
        {
            var ratioWidth = Math.Max(1, _configuredViewportWidth);
            var ratioHeight = Math.Max(1, _configuredViewportHeight);
            var scale = Math.Min((double)targetWidth / ratioWidth, (double)targetHeight / ratioHeight);
            if (scale <= 0)
                scale = 1;

            targetWidth = Math.Max(320, (int)Math.Round(ratioWidth * scale));
            targetHeight = Math.Max(240, (int)Math.Round(ratioHeight * scale));
        }

        await ApplyViewportSizeInternalAsync(targetWidth, targetHeight);
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

    /// <summary>
    /// 获取当前上下文中所有标签页的视图信息列表（供前端展示）。
    /// </summary>
    /// <returns>标签页信息列表。</returns>
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

            tabs.Add(new BrowserTabInfo(
                id,
                title,
                page.Url,
                activePage == page,
                InstanceId,
                DisplayName,
                Color,
                OwnerPluginId,
                false));
        }

        return tabs;
    }

    /// <summary>
    /// 将指定标签页设为选中并尝试调整视口与置前操作。
    /// </summary>
    /// <param name="pageId">要选中的页面 ID。</param>
    /// <returns>是否成功选中页面。</returns>
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

    /// <summary>
    /// 新建标签页并可选地导航到指定 URL。
    /// </summary>
    /// <param name="url">可选的初始导航地址。</param>
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

    /// <summary>
    /// 关闭指定的标签页并尝试恢复到合适的选中页。
    /// </summary>
    /// <param name="pageId">要关闭的页面 ID。</param>
    /// <returns>是否成功关闭。</returns>
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

    /// <summary>
    /// 关闭当前上下文中所有标签页并清理选中状态。
    /// </summary>
    /// <returns>是否所有页面都成功关闭。</returns>
    public async Task<bool> CloseAllTabsAsync()
    {
        CleanupTrackedPages();

        var pages = BrowserContext.Pages.ToList();
        var allClosed = true;

        foreach (var page in pages)
        {
            try
            {
                await page.CloseAsync();
            }
            catch
            {
                allClosed = false;
            }
        }

        CleanupTrackedPages();
        SelectedPageId = null;
        return allClosed;
    }

    /// <summary>
    /// 获取当前活动页面的标题（若可用）。
    /// </summary>
    /// <returns>页面标题或 null。</returns>
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

    /// <summary>
    /// 导航当前活动页面到指定地址或使用搜索模板进行搜索。
    /// </summary>
    /// <param name="url">目标地址或搜索关键词。</param>
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
            await page.GotoAsync(uri?.AbsoluteUri ?? "about:blank");
        }
        catch (PlaywrightException ex)
        {
            LastErrorMessage = $"导航失败：{ex.Message}";
        }
    }

    /// <summary>
    /// 在当前活动页面后退。
    /// </summary>
    /// <returns>是否成功后退到上一页。</returns>
    public async Task<bool> GoBackAsync()
    {
        var page = ActivePage;
        if (page == null) return false;
        return await page.GoBackAsync() != null;
    }

    /// <summary>
    /// 在当前活动页面前进。
    /// </summary>
    /// <returns>是否成功前进到下一页。</returns>
    public async Task<bool> GoForwardAsync()
    {
        var page = ActivePage;
        if (page == null) return false;
        return await page.GoForwardAsync() != null;
    }

    /// <summary>
    /// 重新加载当前活动页面。
    /// </summary>
    public async Task ReloadAsync()
    {
        var page = ActivePage;
        if (page == null) return;
        await page.ReloadAsync();
    }

    /// <summary>
    /// 捕获当前活动页面的截图并以字节数组形式返回（PNG）。
    /// </summary>
    /// <returns>截图字节数组或 null（无活动页面时）。</returns>
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

    /// <summary>
    /// 设置用户配置的视口尺寸并将其应用到页面。
    /// </summary>
    /// <param name="width">视口宽度（像素）。</param>
    /// <param name="height">视口高度（像素）。</param>
    public async Task SetViewportSizeAsync(int width, int height)
    {
        _configuredViewportWidth = Math.Max(320, width);
        _configuredViewportHeight = Math.Max(240, height);
        await ApplyConfiguredViewportSizeAsync();
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

