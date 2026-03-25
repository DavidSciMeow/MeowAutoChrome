using Microsoft.Playwright;
using Microsoft.Extensions.Logging;
using MeowAutoChrome.Contracts.BrowserContext;
using System.Net;
using MeowAutoChrome.Core.Services;
using System.Collections.Concurrent;

namespace MeowAutoChrome.Core;

public class PlaywrightInstance : IScreencastable
{
    // Minimal wrapper adapted from original PlayWrightWarpper. Full feature parity will be migrated gradually.
    private readonly ILogger<PlaywrightInstance> _logger;
    private IBrowserContext? _context;
    private readonly ConcurrentDictionary<string, IPage> _pagesById = new();
    private static readonly Lazy<Task<IPlaywright>> _sharedPlaywrightLazy = new(() => Playwright.CreateAsync());
    public string OwnerId { get; }
    public string? LastErrorMessage { get; set; }
    public string InstanceId { get; }
    public string DisplayName { get; }
    public bool IsHeadless { get; private set; }
    public string UserDataDirectoryPath { get; set; } = string.Empty;
    public string? SelectedPageId { get; set; }
    public IReadOnlyList<IPage> Pages => _pagesById.Values.ToList().AsReadOnly();
    public IReadOnlyList<string> TabIds => _pagesById.Keys.ToList().AsReadOnly();

    public IPage? GetPageById(string tabId) => _pagesById.TryGetValue(tabId, out var p) ? p : null;

    public PlaywrightInstance(ILogger<PlaywrightInstance> logger, string instanceId, string displayName, string ownerId)
    {
        _logger = logger;
        InstanceId = instanceId;
        DisplayName = displayName;
        OwnerId = ownerId;
    }

    public Task<ICDPSession?> CreateCdpSessionAsync(IPage page)
        => BrowserContext is null ? Task.FromResult<ICDPSession?>(null) : BrowserContext.NewCDPSessionAsync(page);

    public Task DispatchMouseEventAsync(ICDPSession session, object args)
        => session.SendAsync("Input.dispatchMouseEvent", (Dictionary<string, object>?)args);

    public Task DispatchKeyEventAsync(ICDPSession session, object args)
        => session.SendAsync("Input.dispatchKeyEvent", (Dictionary<string, object>?)args);

    public IBrowserContext? BrowserContext => _context;

    public async Task InitializeAsync(string userDataDir, bool headless, string? userAgent = null)
    {
        // remember the user data path for this instance so callers can read it later
        UserDataDirectoryPath = userDataDir;
        Directory.CreateDirectory(userDataDir);
        try
        {
            var playwright = await GetSharedPlaywrightAsync();
            _context = await playwright.Chromium.LaunchPersistentContextAsync(userDataDir, new BrowserTypeLaunchPersistentContextOptions
        {
            Headless = headless,
            UserAgent = userAgent,
            ViewportSize = null,
            Args = ["--no-first-run", "--disable-blink-features=AutomationControlled"]
        });
        }
        catch (Exception ex)
        {
            LastErrorMessage = ex.Message;
            _logger.LogError(ex, "Failed to initialize Playwright instance {InstanceId}", InstanceId);
            throw;
        }

        // Map existing pages from context to generated ids
        foreach (var page in _context.Pages)
        {
            var id = Guid.NewGuid().ToString("N");
            _pagesById[id] = page;
        }

        IsHeadless = headless;
        _logger.LogInformation("Playwright instance initialized {InstanceId} headless={Headless}", InstanceId, IsHeadless);
    }

    private static Task<IPlaywright> GetSharedPlaywrightAsync() => _sharedPlaywrightLazy.Value;

    public async Task CloseAsync()
    {
        try
        {
            if (_context != null)
                await _context.CloseAsync();
            _pagesById.Clear();
        }
        catch { }
    }

    public string CreateTabId(IPage page)
    {
        var id = Guid.NewGuid().ToString("N");
        _pagesById[id] = page;
        return id;
    }

    public async Task<string> CreateTabAsync(string? url = null)
    {
        if (BrowserContext is null) throw new InvalidOperationException("Browser context not initialized");
        var page = await BrowserContext.NewPageAsync();
        if (!string.IsNullOrWhiteSpace(url))
        {
            try { await page.GotoAsync(url); } catch { }
        }
        var id = CreateTabId(page);
        SelectedPageId = id;
        return id;
    }

    public bool TryGetPage(string tabId, out IPage? page) => _pagesById.TryGetValue(tabId, out page);

    public async Task<bool> CloseTabAsync(string tabId)
    {
        if (!_pagesById.TryRemove(tabId, out var page)) return false;
        try { await page.CloseAsync(); } catch { }
        if (SelectedPageId == tabId) SelectedPageId = _pagesById.Keys.FirstOrDefault();
        return true;
    }

    public bool SelectPage(string tabId)
    {
        if (!_pagesById.ContainsKey(tabId)) return false;
        SelectedPageId = tabId;
        return true;
    }

    public IPage? GetSelectedPage() => SelectedPageId is null ? null : (_pagesById.TryGetValue(SelectedPageId, out var p) ? p : null);
}
