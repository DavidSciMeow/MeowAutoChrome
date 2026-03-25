using MeowAutoChrome.Contracts.BrowserPlugin;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MeowAutoChrome.Core.Interface;

public interface IPluginHostCore
{
    string PluginRootPath { get; }
    void EnsurePluginDirectoryExists();
    BrowserPluginCatalogResponse GetPluginCatalog();
    IReadOnlyList<BrowserPluginDescriptor?> GetPlugins();
    Task<BrowserPluginExecutionResponse?> ControlAsync(string pluginId, string command, IReadOnlyDictionary<string, string?>? arguments, string? connectionId = null, CancellationToken cancellationToken = default);
    Task<BrowserPluginExecutionResponse?> ExecuteAsync(string pluginId, string functionId, IReadOnlyDictionary<string, string?>? arguments, string? connectionId = null, CancellationToken cancellationToken = default);
}
