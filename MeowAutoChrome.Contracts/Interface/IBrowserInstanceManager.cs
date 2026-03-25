using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MeowAutoChrome.Contracts.BrowserContext;
using Microsoft.Playwright;

namespace MeowAutoChrome.Contracts.Interface;

/// <summary>
/// 浏览器实例管理器接口，提供对浏览器实例的管理和操作能力，包括获取实例信息、创建和删除实例、选择实例等功能，插件可以通过这个接口与宿主进行交互，获取当前浏览器实例的信息或执行跨实例的操作
/// </summary>
public interface IBrowserInstanceManager : IBrowserInstanceQuery
{
    // Mutating/management operations
    Task CreateTabAsync(string? url = null);
    Task<string?> GetTitleAsync();
    Task NavigateAsync(string url);
    Task GoBackAsync();
    Task GoForwardAsync();
    Task ReloadAsync();
    Task<byte[]?> CaptureScreenshotAsync();
    Task SetViewportSizeAsync(int width, int height);
    Task UpdateLaunchSettingsAsync(string primaryUserDataDirectory, bool isHeadless, bool forceReload = false);

    // Keep both old and new signatures for UpdateInstanceSettingsAsync for backward compatibility.
    Task<bool> UpdateInstanceSettingsAsync(MeowAutoChrome.Contracts.BrowserContext.BrowserInstanceSettingsUpdateRequest request, CancellationToken cancellationToken = default);
    Task<bool> UpdateInstanceSettingsAsync(string instanceId, string userDataDirectory, int viewportWidth, int viewportHeight, bool autoResizeViewport, bool preserveAspectRatio, bool useProgramUserAgent, string? userAgent, bool migrateExistingUserData, int? displayWidth = null, int? displayHeight = null, CancellationToken cancellationToken = default);

    Task SyncCurrentInstanceViewportAsync(int width, int height, CancellationToken cancellationToken = default);
    Task<bool> CloseTabAsync(string tabId);
    Task<bool> CloseBrowserInstanceAsync(string instanceId, CancellationToken cancellationToken = default);
    Task<bool> SelectPageAsync(string tabId);
    Task<string> CreateBrowserInstanceAsync(string ownerPluginId, string? displayName = null, string? userDataDirectory = null, string? previewInstanceId = null, CancellationToken cancellationToken = default);
    Task<bool> RemoveBrowserInstanceAsync(string instanceId, CancellationToken cancellationToken = default);
    Task<bool> SelectBrowserInstanceAsync(string instanceId, CancellationToken cancellationToken = default);
    Task<(string InstanceId, string UserDataDirectory)> PreviewNewInstanceAsync(string? ownerPluginId, string? userDataDirectoryRoot);
}
