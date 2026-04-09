using System.Collections.ObjectModel;
using MeowAutoChrome.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace MeowAutoChrome.Core.Services.PluginHost;

/// <summary>
/// Core 管理的插件宿主上下文实现。它同时实现插件上下文与宿主 facade，向插件暴露当前实例、宿主基础地址以及实例管理能力。<br/>
/// Core-owned plugin host context implementation. It implements both the plugin context and host facade, exposing the current instance, host base address, and instance-management capabilities to plugins.
/// </summary>
public sealed class PluginHostContextCore : IPluginContext, IPluginHost
{
    private readonly Func<string?, IReadOnlyDictionary<string, string?>?, bool, Task>? _publishUpdate;
    private readonly Func<CancellationToken, Task<IReadOnlyList<IPluginBrowserInstance>>>? _getOwnedBrowserInstances;
    private readonly Func<string, CancellationToken, Task<IPluginBrowserInstance?>>? _getBrowserInstance;
    private readonly Func<BrowserCreationOptions, CancellationToken, Task<IPluginBrowserInstance?>>? _createBrowserInstance;
    private readonly Func<string, CancellationToken, Task<bool>>? _closeBrowserInstance;
    private readonly Func<string, CancellationToken, Task<bool>>? _selectBrowserInstance;
    private readonly Func<LogLevel, string, string?, Task>? _logCallback;
    private readonly List<IPluginBrowserInstance> _instances;
    private readonly ReadOnlyCollection<IPluginBrowserInstance> _instancesView;
    private IPluginBrowserInstance? _currentBrowserInstance;

    public PluginHostContextCore(
        IPluginBrowserInstance? currentBrowserInstance,
        IReadOnlyList<IPluginBrowserInstance>? instances,
        IReadOnlyDictionary<string, string?> arguments,
        string pluginId,
        string targetId,
        PluginState state,
        string? baseAddress,
        Func<string?, IReadOnlyDictionary<string, string?>?, bool, Task>? publishUpdate,
        Func<CancellationToken, Task<IReadOnlyList<IPluginBrowserInstance>>>? getOwnedBrowserInstances = null,
        Func<string, CancellationToken, Task<IPluginBrowserInstance?>>? getBrowserInstance = null,
        Func<BrowserCreationOptions, CancellationToken, Task<IPluginBrowserInstance?>>? createBrowserInstance = null,
        Func<string, CancellationToken, Task<bool>>? closeBrowserInstance = null,
        Func<string, CancellationToken, Task<bool>>? selectBrowserInstance = null,
        Func<LogLevel, string, string?, Task>? logCallback = null,
        CancellationToken cancellationToken = default)
    {
        _currentBrowserInstance = currentBrowserInstance;
        _instances = instances?.ToList() ?? [];
        _instancesView = _instances.AsReadOnly();
        Arguments = arguments ?? new Dictionary<string, string?>();
        PluginId = pluginId;
        TargetId = targetId;
        State = state;
        BaseAddress = baseAddress;
        _publishUpdate = publishUpdate;
        _getOwnedBrowserInstances = getOwnedBrowserInstances;
        _getBrowserInstance = getBrowserInstance;
        _createBrowserInstance = createBrowserInstance;
        _closeBrowserInstance = closeBrowserInstance;
        _selectBrowserInstance = selectBrowserInstance;
        _logCallback = logCallback;
        CancellationToken = cancellationToken;
    }

    public IPluginHost Host => this;

    public IPluginBrowserInstance? CurrentBrowserInstance => _currentBrowserInstance;

    public IReadOnlyList<IPluginBrowserInstance> Instances => _instancesView;

    public IBrowserContext? BrowserContext => CurrentBrowserInstance?.BrowserContext;

    public IBrowser? Browser => CurrentBrowserInstance?.Browser;

    public IPage? ActivePage => CurrentBrowserInstance?.ActivePage;

    public string BrowserInstanceId => CurrentBrowserInstance?.InstanceId ?? string.Empty;

    public IReadOnlyDictionary<string, string?> Arguments { get; }

    public string PluginId { get; }

    public string TargetId { get; }

    public PluginState State { get; }

    public string? BaseAddress { get; }

    public CancellationToken CancellationToken { get; }

    public Task PublishUpdateAsync(string? message, IReadOnlyDictionary<string, string?>? data = null, bool toastRequested = false)
        => _publishUpdate?.Invoke(message, data, toastRequested) ?? Task.CompletedTask;

    public Task ToastAsync(string message, IReadOnlyDictionary<string, string?>? data = null)
        => PublishUpdateAsync(message, data, toastRequested: true);

    public Task<IReadOnlyList<IPluginBrowserInstance>> GetOwnedBrowserInstancesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<IPluginBrowserInstance>>(Instances);

    public Task<IPluginBrowserInstance?> GetBrowserInstanceAsync(string instanceId, CancellationToken cancellationToken = default)
        => _getBrowserInstance is null ? Task.FromResult<IPluginBrowserInstance?>(null) : _getBrowserInstance(instanceId, cancellationToken);

    public async Task<IPluginBrowserInstance?> CreateBrowserInstanceAsync(BrowserCreationOptions options, CancellationToken cancellationToken = default)
    {
        if (_createBrowserInstance is null)
            return null;

        var created = await _createBrowserInstance(options, cancellationToken);
        if (created is null)
            return null;

        await RefreshInstancesAsync(cancellationToken);
        return FindInstance(created.InstanceId) ?? created;
    }

    public async Task<bool> CloseBrowserInstanceAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        if (_closeBrowserInstance is null)
            return false;

        var closed = await _closeBrowserInstance(instanceId, cancellationToken);
        if (!closed)
            return false;

        RemoveInstance(instanceId);
        if (string.Equals(_currentBrowserInstance?.InstanceId, instanceId, StringComparison.Ordinal))
            _currentBrowserInstance = null;

        await RefreshInstancesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> SelectBrowserInstanceAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        if (_selectBrowserInstance is null)
            return false;

        var selected = await _selectBrowserInstance(instanceId, cancellationToken);
        if (!selected)
            return false;

        await RefreshInstancesAsync(cancellationToken);
        _currentBrowserInstance = FindInstance(instanceId) ?? await GetBrowserInstanceAsync(instanceId, cancellationToken);
        return true;
    }

    public Task WriteLogAsync(string level, string message, string? category = null)
    {
        if (_logCallback is null)
            return Task.CompletedTask;

        try
        {
            if (Enum.TryParse<LogLevel>(level, true, out var parsed))
                return _logCallback(parsed, message, category ?? PluginId);

            if (int.TryParse(level, out var num) && Enum.IsDefined(typeof(LogLevel), num))
                return _logCallback((LogLevel)num, message, category ?? PluginId);
        }
        catch
        {
        }

        return _logCallback(LogLevel.Information, message, category ?? PluginId);
    }

    private async Task RefreshInstancesAsync(CancellationToken cancellationToken)
    {
        if (_getOwnedBrowserInstances is null)
            return;

        var instances = await _getOwnedBrowserInstances(cancellationToken);
        _instances.Clear();
        _instances.AddRange(instances);

        if (_currentBrowserInstance is not null)
        {
            var refreshedCurrent = FindInstance(_currentBrowserInstance.InstanceId);
            if (refreshedCurrent is not null)
                _currentBrowserInstance = refreshedCurrent;
        }
    }

    private IPluginBrowserInstance? FindInstance(string instanceId)
        => _instances.FirstOrDefault(instance => string.Equals(instance.InstanceId, instanceId, StringComparison.Ordinal));

    private void RemoveInstance(string instanceId)
    {
        var index = _instances.FindIndex(instance => string.Equals(instance.InstanceId, instanceId, StringComparison.Ordinal));
        if (index >= 0)
            _instances.RemoveAt(index);
    }
}

internal sealed class PluginBrowserPageHandleCore(string pageId, IPage page, bool isSelected) : IPluginBrowserPage
{
    public string PageId { get; } = pageId;
    public IPage Page { get; } = page;
    public bool IsSelected { get; } = isSelected;
}

internal sealed class PluginBrowserInstanceHandleCore : IPluginBrowserInstance
{
    public PluginBrowserInstanceHandleCore(string instanceId, string? displayName, string? userDataDirectory, string? ownerId, bool isCurrent, IBrowser? browser, IBrowserContext? browserContext, IPage? activePage, string? selectedPageId, IReadOnlyList<IPluginBrowserPage> pages)
    {
        InstanceId = instanceId;
        DisplayName = displayName;
        UserDataDirectory = userDataDirectory;
        OwnerId = ownerId;
        IsCurrent = isCurrent;
        Browser = browser;
        BrowserContext = browserContext;
        ActivePage = activePage;
        SelectedPageId = selectedPageId;
        Pages = pages;
    }

    public string InstanceId { get; }
    public string? DisplayName { get; }
    public string? UserDataDirectory { get; }
    public string? OwnerId { get; }
    public bool IsCurrent { get; }
    public IBrowser? Browser { get; }
    public IBrowserContext? BrowserContext { get; }
    public IPage? ActivePage { get; }
    public string? SelectedPageId { get; }
    public IReadOnlyList<IPluginBrowserPage> Pages { get; }
}
