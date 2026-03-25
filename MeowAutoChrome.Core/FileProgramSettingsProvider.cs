using System.Text.Json;

namespace MeowAutoChrome.Core.Interface;

public sealed class FileProgramSettingsProvider : IProgramSettingsProvider
{
    private readonly SemaphoreSlim _semaphore = new(1,1);
    private readonly string _settingsFilePath = MeowAutoChrome.Core.Struct.ProgramSettings.GetSettingsFilePath();
    private readonly string _legacySettingsFilePath = MeowAutoChrome.Core.Struct.ProgramSettings.GetLegacySettingsFilePath();
    private MeowAutoChrome.Core.Struct.ProgramSettings? _cachedSettings;

    public async Task<MeowAutoChrome.Core.Struct.ProgramSettings> GetAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            EnsureSettingsFileMigrated();
            if (_cachedSettings != null)
                return Clone(_cachedSettings);

            if (!File.Exists(_settingsFilePath))
            {
                _cachedSettings = new MeowAutoChrome.Core.Struct.ProgramSettings();
                return Clone(_cachedSettings);
            }

            await using var stream = File.OpenRead(_settingsFilePath);
            _cachedSettings = await JsonSerializer.DeserializeAsync<MeowAutoChrome.Core.Struct.ProgramSettings>(stream) ?? new MeowAutoChrome.Core.Struct.ProgramSettings();
            Normalize(_cachedSettings);
            return Clone(_cachedSettings);
        }
        finally { _semaphore.Release(); }
    }

    public async Task SaveAsync(MeowAutoChrome.Core.Struct.ProgramSettings settings)
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

    public async Task InjectCustomSettingsAsync(System.Collections.Generic.IDictionary<string, string?> customSettings)
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

    private static void Normalize(MeowAutoChrome.Core.Struct.ProgramSettings settings)
    {
        settings.SearchUrlTemplate = string.IsNullOrWhiteSpace(settings.SearchUrlTemplate) ? MeowAutoChrome.Core.Struct.ProgramSettings.DefaultSearchUrlTemplate : settings.SearchUrlTemplate.Trim();
        settings.UserDataDirectory = string.IsNullOrWhiteSpace(settings.UserDataDirectory) ? MeowAutoChrome.Core.Struct.ProgramSettings.GetDefaultUserDataDirectoryPath() : Path.GetFullPath(settings.UserDataDirectory);
        settings.UserAgent = string.IsNullOrWhiteSpace(settings.UserAgent) ? null : settings.UserAgent.Trim();
        settings.ScreencastFps = Math.Clamp(settings.ScreencastFps <= 0 ? MeowAutoChrome.Core.Struct.ProgramSettings.DefaultScreencastFps : settings.ScreencastFps, 1, 60);
        settings.PluginPanelWidth = Math.Clamp(settings.PluginPanelWidth <= 0 ? MeowAutoChrome.Core.Struct.ProgramSettings.DefaultPluginPanelWidth : settings.PluginPanelWidth, MeowAutoChrome.Core.Struct.ProgramSettings.MinPluginPanelWidth, MeowAutoChrome.Core.Struct.ProgramSettings.MaxPluginPanelWidth);
    }

    private void EnsureSettingsFileMigrated()
    {
        if (File.Exists(_settingsFilePath) || !File.Exists(_legacySettingsFilePath))
            return;

        Directory.CreateDirectory(Path.GetDirectoryName(_settingsFilePath)!);
        File.Move(_legacySettingsFilePath, _settingsFilePath);
    }

    private static MeowAutoChrome.Core.Struct.ProgramSettings Clone(MeowAutoChrome.Core.Struct.ProgramSettings settings)
        => JsonSerializer.Deserialize<MeowAutoChrome.Core.Struct.ProgramSettings>(JsonSerializer.Serialize(settings)) ?? new MeowAutoChrome.Core.Struct.ProgramSettings();
}
