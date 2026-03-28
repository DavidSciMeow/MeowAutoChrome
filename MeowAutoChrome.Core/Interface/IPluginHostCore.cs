using MeowAutoChrome.Core.Models;

namespace MeowAutoChrome.Core.Interface;

public interface IPluginHostCore
{
    string PluginRootPath { get; }
    void EnsurePluginDirectoryExists();
    BrowserPluginCatalogResponse GetPluginCatalog();
    IReadOnlyList<BrowserPluginDescriptor?> GetPlugins();
    Task<BrowserPluginExecutionResponse?> ControlAsync(string pluginId, string command, IReadOnlyDictionary<string, string?>? arguments, string? connectionId = null, CancellationToken cancellationToken = default);
    Task<BrowserPluginExecutionResponse?> ExecuteAsync(string pluginId, string functionId, IReadOnlyDictionary<string, string?>? arguments, string? connectionId = null, CancellationToken cancellationToken = default);
    /// <summary>
    /// Trigger an immediate plugin scan and return the catalog result.
    /// </summary>
    Task<BrowserPluginCatalogResponse> ScanPluginsAsync(CancellationToken cancellationToken = default);
    Task<BrowserPluginCatalogResponse> LoadPluginAssemblyAsync(string pluginPath, CancellationToken cancellationToken = default);
    Task<(bool Success, IReadOnlyList<string> Errors)> UnloadPluginAsync(string pluginId, CancellationToken cancellationToken = default);
    Task<(string InstanceId, string? UserDataDirectory)?> PreviewNewInstanceAsync(string ownerId, string? root = null);
    Task<bool> CloseBrowserInstanceAsync(string instanceId, CancellationToken cancellationToken = default);
}
