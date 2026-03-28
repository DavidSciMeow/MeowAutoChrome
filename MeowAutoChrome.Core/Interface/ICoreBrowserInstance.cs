using Microsoft.Playwright;

namespace MeowAutoChrome.Core.Interface;

/// <summary>
/// Core-internal abstraction for a browser instance to avoid depending on Web-specific implementations.
/// Named with Core prefix to avoid conflicts with Contracts.
/// </summary>
public interface ICoreBrowserInstance
{
    string OwnerId { get; }
    string InstanceId { get; }
    string DisplayName { get; }
    bool IsHeadless { get; }
    string UserDataDirectoryPath { get; set; }
    string? SelectedPageId { get; set; }
    IReadOnlyList<IPage> Pages { get; }
    IReadOnlyList<string> TabIds { get; }

    IBrowserContext? BrowserContext { get; }
    IPage? GetSelectedPage();
    IPage? GetPageById(string tabId);

    // CDP / input helpers (define here to avoid depending on Core.Services namespace and
    // therefore break namespace mutual dependency between Core.Interface and Core.Services)
    Task<ICDPSession?> CreateCdpSessionAsync(IPage page);
    Task DispatchMouseEventAsync(ICDPSession session, object payload);
    Task DispatchKeyEventAsync(ICDPSession session, object payload);

    Task InitializeAsync(string userDataDir, bool headless, string? userAgent = null);
    Task CloseAsync();
    Task<string> CreateTabAsync(string? url = null);
    Task<bool> CloseTabAsync(string tabId);
    bool SelectPage(string tabId);
}
