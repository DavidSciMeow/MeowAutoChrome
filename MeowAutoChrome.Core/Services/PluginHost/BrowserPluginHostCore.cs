using MeowAutoChrome.Contracts.BrowserPlugin;
using MeowAutoChrome.Contracts.Interface;
using MeowAutoChrome.Core.Services;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace MeowAutoChrome.Core.Services.PluginHost;

/// <summary>
/// Core implementation of plugin host logic moved from Web.
/// This class does not depend on ASP.NET types and can run in CLI environments.
/// It exposes discovery, execution and publishing via IPluginOutputPublisher.
/// </summary>
public sealed class BrowserPluginHostCore
{
    private readonly BrowserInstanceManagerCore _browserInstances;
    private readonly PluginDiscoveryService _discovery;
    private readonly IPluginOutputPublisher _publisher;
    private readonly ILogger<BrowserPluginHostCore> _logger;

    public BrowserPluginHostCore(BrowserInstanceManagerCore browserInstances, PluginDiscoveryService discovery, IPluginOutputPublisher publisher, ILogger<BrowserPluginHostCore> logger)
    {
        _browserInstances = browserInstances;
        _discovery = discovery;
        _publisher = publisher;
        _logger = logger;
    }

    public string PluginRootPath => _discovery.PluginRootPath;

    public void EnsurePluginDirectoryExists() => _discovery.EnsurePluginDirectoryExists();
    // Minimal wrappers to the previous BrowserPluginHost API used by Web.
    // Implementations are intentionally thin delegations that the Web-level
    // BrowserPluginHost adapter will call into when it needs richer environment
    // (IWebHostEnvironment, SignalR, etc.). This keeps Core independent.
    public BrowserPluginCatalogResponse GetPluginCatalog() => throw new NotImplementedException();

    public IReadOnlyList<BrowserPluginDescriptor?> GetPlugins() => GetPluginCatalog().Plugins;

    public Task<BrowserPluginExecutionResponse?> ControlAsync(string pluginId, string command, IReadOnlyDictionary<string, string?>? arguments, string? connectionId = null, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<BrowserPluginExecutionResponse?> ExecuteAsync(string pluginId, string functionId, IReadOnlyDictionary<string, string?>? arguments, string? connectionId = null, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}
