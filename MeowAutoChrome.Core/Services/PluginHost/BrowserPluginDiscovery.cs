using CoreModels = MeowAutoChrome.Core.Models;
using MeowAutoChrome.Core.Interface;
using MeowAutoChrome.Core.Services.PluginDiscovery;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace MeowAutoChrome.Core.Services.PluginHost;

    internal sealed class BrowserPluginDiscovery
    {
        private readonly IPluginDiscoveryService _discovery;
    private readonly MeowAutoChrome.Core.Interface.ICorePluginAssemblyLoader _assemblyLoader;
        private readonly ILogger _logger;
        private readonly IPluginInstanceManager _instanceManager;

        private CoreModels.PluginDiscoverySnapshot _latestSnapshot = new CoreModels.PluginDiscoverySnapshot(Array.Empty<CoreModels.RuntimeBrowserPlugin>(), Array.Empty<string>(), Array.Empty<CoreModels.BrowserPluginErrorDescriptor>());
        private readonly object _snapshotLock = new();
        private readonly TimeSpan _scanInterval = TimeSpan.FromSeconds(30);

        public BrowserPluginDiscovery(IPluginDiscoveryService discovery, MeowAutoChrome.Core.Interface.ICorePluginAssemblyLoader assemblyLoader, ILogger logger, IPluginInstanceManager instanceManager)
        {
            _discovery = discovery;
            _assemblyLoader = assemblyLoader;
            _logger = logger;
            _instanceManager = instanceManager;

            try
            {
                _latestSnapshot = _discovery.DiscoverAll(_assemblyLoader);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Initial plugin discovery failed.");
                _latestSnapshot = new CoreModels.PluginDiscoverySnapshot(Array.Empty<CoreModels.RuntimeBrowserPlugin>(), Array.Empty<string>(), Array.Empty<CoreModels.BrowserPluginErrorDescriptor>());
            }
        }

        public string PluginRootPath => _discovery.PluginRootPath;
        public void EnsurePluginDirectoryExists() => _discovery.EnsurePluginDirectoryExists();

        public CoreModels.PluginDiscoverySnapshot GetLatestSnapshot()
        {
            lock (_snapshotLock) return _latestSnapshot;
        }

        public CoreModels.PluginDiscoverySnapshot DiscoverPluginsCore()
        {
            EnsurePluginDirectoryExists();

            var plugins = new List<CoreModels.RuntimeBrowserPlugin>();
            var errors = new List<string>();
            var errorsDetailed = new List<CoreModels.BrowserPluginErrorDescriptor>();

            foreach (var pluginPath in Directory.EnumerateFiles(PluginRootPath, "*.dll", SearchOption.AllDirectories))
            {
                string[] candidateTypeNames;

                try
                {
                    candidateTypeNames = PluginMetadataScanner.DiscoverPluginTypeNames(pluginPath);
                }
                catch (Exception ex)
                {
                    var detail = ex.ToString();
                    var message = $"插件程序集 {Path.GetFileName(pluginPath)} 元数据扫描失败：{detail}";
                    _logger.LogError(ex, "插件程序集 {PluginAssembly} 元数据扫描失败。", pluginPath);
                    errors.Add(message);
                    errorsDetailed.Add(new CoreModels.BrowserPluginErrorDescriptor(Path.GetFileName(pluginPath), ex.Message, detail));
                    continue;
                }

                if (candidateTypeNames.Length == 0)
                    continue;

                var assembly = _assemblyLoader.Load(pluginPath, errors);
                if (assembly is null)
                    continue;

                var discovered = DiscoverPlugins(assembly, pluginPath, candidateTypeNames, errors);
                plugins.AddRange(discovered);
                _assemblyLoader.RegisterPlugins(pluginPath, discovered.Select(p => p.Id));
            }

            var snapshot = new CoreModels.PluginDiscoverySnapshot(
                plugins
                    .Where(plugin => plugin.Actions.Count > 0 || plugin.Controls.Count > 0)
                    .OrderBy(plugin => plugin.Name, StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                errors.ToArray(),
                errorsDetailed.ToArray());

            lock (_snapshotLock)
            {
                _latestSnapshot = snapshot;
            }

            return snapshot;
        }

        public CoreModels.BrowserPluginCatalogResponse LoadPluginAssembly(string pluginPath)
        {
            var (plugins, errors, errorsDetailed) = _discovery.DiscoverFromAssembly(pluginPath, _assemblyLoader);

            lock (_snapshotLock)
            {
                var merged = _latestSnapshot.Plugins.Concat(plugins).ToArray();
                _latestSnapshot = new CoreModels.PluginDiscoverySnapshot(merged, _latestSnapshot.Errors.Concat(errors).ToArray(), _latestSnapshot.ErrorsDetailed);
            }

            var outErrors = new List<string>(errors);
                var descriptors = plugins.Select(p => BrowserPluginHostCoreHelpers.CreatePluginDescriptor(p, outErrors, _instanceManager)).Where(d => d is not null).ToArray()!;
            return new CoreModels.BrowserPluginCatalogResponse(descriptors.Select(d => d!).ToArray(), outErrors, errorsDetailed.ToArray());
        }

        public (bool Success, IReadOnlyList<string> Errors) UnloadPlugin(string pluginId)
        {
            var errors = new List<string>();
            try
            {
                var path = _assemblyLoader.GetAssemblyPathForPluginId(pluginId);
                if (path is null)
                {
                    errors.Add($"Plugin id {pluginId} not found or not loaded.");
                    return (false, errors);
                }

                _instanceManager.RemoveInstanceByPluginId(pluginId);
                _assemblyLoader.UnregisterPlugins(path);
                _assemblyLoader.Unload(path);

            lock (_snapshotLock)
            {
                var remaining = _latestSnapshot.Plugins.Where(p => p.Id != pluginId).ToArray();
                _latestSnapshot = new CoreModels.PluginDiscoverySnapshot(remaining, _latestSnapshot.Errors, _latestSnapshot.ErrorsDetailed);
            }

                return (true, Array.Empty<string>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to unload plugin {PluginId}", pluginId);
                errors.Add(ex.Message);
                return (false, errors);
            }
        }

        public List<CoreModels.RuntimeBrowserPlugin> DiscoverPlugins(Assembly assembly, string pluginPath, IReadOnlyList<string> candidateTypeNames, List<string> errors)
        {
            var plugins = new List<CoreModels.RuntimeBrowserPlugin>();

            foreach (var candidateTypeName in candidateTypeNames)
            {
                try
                {
                    var type = assembly.GetType(candidateTypeName, throwOnError: false, ignoreCase: false);
                    if (type is not { IsAbstract: false, IsInterface: false } || !typeof(MeowAutoChrome.Contracts.IPlugin).IsAssignableFrom(type))
                        continue;

                    var pluginAttribute = type.GetCustomAttribute<MeowAutoChrome.Contracts.Attributes.PluginAttribute>();
                    if (pluginAttribute is null)
                        continue;

                    plugins.Add(new CoreModels.RuntimeBrowserPlugin(
                        pluginAttribute.Id,
                        pluginAttribute.Name,
                        pluginAttribute.Description,
                        type,
                        BrowserPluginHostCoreHelpers.DiscoverControls(type),
                        BrowserPluginHostCoreHelpers.DiscoverActions(type)));
                }
                catch (Exception ex)
                {
                    var detail = ex.ToString();
                    var message = $"插件类型 {candidateTypeName} 发现失败：{detail}";
                    _logger.LogError(ex, "插件类型 {PluginType} 发现失败。插件程序集：{PluginAssembly}", candidateTypeName, pluginPath);
                    errors.Add(message);
                }
            }

            return plugins;
        }

        public async Task ScanLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var snapshot = _discovery.DiscoverAll(_assemblyLoader);
                    lock (_snapshotLock)
                    {
                        _latestSnapshot = snapshot;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Background plugin scan failed.");
                }

                try { await Task.Delay(_scanInterval, cancellationToken); } catch { }
            }
        }
    }
