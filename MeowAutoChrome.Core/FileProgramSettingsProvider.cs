using System.Text.Json;

namespace MeowAutoChrome.Core.Interface;

/// <summary>
/// 基于文件的程序设置提供者，负责从磁盘读取和写入程序设置并提供内存缓存。<br/>
/// File-backed program settings provider responsible for reading/writing program settings from disk with an in-memory cache.
/// </summary>
public sealed class FileProgramSettingsProvider : IProgramSettingsProvider
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly string _settingsFilePath = Struct.ProgramSettings.GetSettingsFilePath();
    private readonly string _legacySettingsFilePath = Struct.ProgramSettings.GetLegacySettingsFilePath();
    private Struct.ProgramSettings? _cachedSettings;

    /// <summary>
    /// 异步获取当前的程序设置，如果配置文件不存在则返回默认设置。<br/>
    /// Asynchronously get the current program settings; returns defaults if no settings file exists.
    /// </summary>
    /// <returns>程序设置对象的副本。<br/>A cloned instance of the program settings.</returns>
    public async Task<Struct.ProgramSettings> GetAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            EnsureSettingsFileMigrated();
            if (_cachedSettings != null)
                return Clone(_cachedSettings);

            if (!File.Exists(_settingsFilePath))
            {
                _cachedSettings = new Struct.ProgramSettings();
                return Clone(_cachedSettings);
            }

            await using var stream = File.OpenRead(_settingsFilePath);
            _cachedSettings = await JsonSerializer.DeserializeAsync<Struct.ProgramSettings>(stream) ?? new Struct.ProgramSettings();
            Normalize(_cachedSettings);
            return Clone(_cachedSettings);
        }
        finally { _semaphore.Release(); }
    }

    /// <summary>
    /// 将提供的设置异步保存到磁盘并更新缓存。<br/>
    /// Asynchronously save the provided settings to disk and update the cache.
    /// </summary>
    /// <param name="settings">要保存的设置对象 / settings to save.</param>
    public async Task SaveAsync(Struct.ProgramSettings settings)
    {
        Normalize(settings);
        await _semaphore.WaitAsync();
        try
        {
            EnsureSettingsFileMigrated();
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsFilePath)!);
            await using var stream = File.Create(_settingsFilePath);
            await JsonSerializer.SerializeAsync(stream, settings, new JsonSerializerOptions { WriteIndented = true });
            _cachedSettings = Clone(settings);
        }
        finally { _semaphore.Release(); }
    }

    /// <summary>
    /// 将自定义键值对注入到当前设置中的 CustomSettings 字典并持久化。<br/>
    /// Inject custom key/value pairs into the current settings' CustomSettings dictionary and persist them.
    /// </summary>
    /// <param name="customSettings">要注入的自定义设置字典 / custom settings to inject.</param>
    public async Task InjectCustomSettingsAsync(IDictionary<string, string?> customSettings)
    {
        if (customSettings is null || customSettings.Count == 0) return;

        await _semaphore.WaitAsync();
        try
        {
            var current = _cachedSettings ?? await GetAsync();
            foreach (var kv in customSettings)
            {
                current.CustomSettings[kv.Key] = kv.Value;
            }

            await SaveAsync(current);
        }
        finally { _semaphore.Release(); }
    }

    private static void Normalize(Struct.ProgramSettings settings)
    {
        settings.SearchUrlTemplate = string.IsNullOrWhiteSpace(settings.SearchUrlTemplate) ? Struct.ProgramSettings.DefaultSearchUrlTemplate : settings.SearchUrlTemplate.Trim();

        // Normalize UserDataDirectory: prefer default if empty, otherwise make absolute.
        settings.UserDataDirectory = string.IsNullOrWhiteSpace(settings.UserDataDirectory)
            ? Struct.ProgramSettings.GetDefaultUserDataDirectoryPath()
            : Path.GetFullPath(settings.UserDataDirectory);

        // Normalize PluginDirectory: if empty use default under AppData; if it's not rooted,
        // interpret it as relative to AppData directory (avoid resolving relative to current working dir).
        if (string.IsNullOrWhiteSpace(settings.PluginDirectory))
        {
            settings.PluginDirectory = Struct.ProgramSettings.GetDefaultPluginDirectoryPath();
        }
        else
        {
            var pluginDirRaw = settings.PluginDirectory.Trim();
            if (Path.IsPathRooted(pluginDirRaw))
            {
                settings.PluginDirectory = Path.GetFullPath(pluginDirRaw);
            }
            else
            {
                var appDataBase = Struct.ProgramSettings.GetAppDataDirectoryPath();
                settings.PluginDirectory = Path.GetFullPath(Path.Combine(appDataBase, pluginDirRaw));
            }
        }

        settings.UserAgent = string.IsNullOrWhiteSpace(settings.UserAgent) ? null : settings.UserAgent.Trim();
        settings.ScreencastFps = Math.Clamp(settings.ScreencastFps <= 0 ? Struct.ProgramSettings.DefaultScreencastFps : settings.ScreencastFps, 1, 60);
        settings.PluginPanelWidth = Math.Clamp(settings.PluginPanelWidth <= 0 ? Struct.ProgramSettings.DefaultPluginPanelWidth : settings.PluginPanelWidth, Struct.ProgramSettings.MinPluginPanelWidth, Struct.ProgramSettings.MaxPluginPanelWidth);
        settings.DisabledPluginAssemblies = (settings.DisabledPluginAssemblies ?? [])
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path =>
            {
                var trimmed = path.Trim();
                if (Path.IsPathRooted(trimmed))
                    return Path.GetFullPath(trimmed);

                return Path.GetFullPath(Path.Combine(settings.PluginDirectory, trimmed));
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private void EnsureSettingsFileMigrated()
    {
        if (File.Exists(_settingsFilePath) || !File.Exists(_legacySettingsFilePath))
            return;

        Directory.CreateDirectory(Path.GetDirectoryName(_settingsFilePath)!);
        File.Move(_legacySettingsFilePath, _settingsFilePath);
    }

    private static Struct.ProgramSettings Clone(Struct.ProgramSettings settings)
        => JsonSerializer.Deserialize<Struct.ProgramSettings>(JsonSerializer.Serialize(settings)) ?? new Struct.ProgramSettings();
}
