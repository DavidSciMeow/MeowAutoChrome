using System.Text.Json;
using MeowAutoChrome.Web.Models;

namespace MeowAutoChrome.Web.Services;

public sealed class ProgramSettingsService(IWebHostEnvironment environment)
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly string _settingsFilePath = Path.Combine(AppContext.BaseDirectory, "program-settings.json");
    private ProgramSettings? _cachedSettings;

    public async Task<ProgramSettings> GetAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            if (_cachedSettings != null)
                return Clone(_cachedSettings);

            if (!File.Exists(_settingsFilePath))
            {
                _cachedSettings = new ProgramSettings();
                return Clone(_cachedSettings);
            }

            await using var stream = File.OpenRead(_settingsFilePath);
            _cachedSettings = await JsonSerializer.DeserializeAsync<ProgramSettings>(stream) ?? new ProgramSettings();
            Normalize(_cachedSettings);
            return Clone(_cachedSettings);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task SaveAsync(ProgramSettings settings)
    {
        Normalize(settings);

        await _semaphore.WaitAsync();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsFilePath)!);
            await using var stream = File.Create(_settingsFilePath);
            await JsonSerializer.SerializeAsync(stream, settings, new JsonSerializerOptions { WriteIndented = true });
            _cachedSettings = Clone(settings);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private static void Normalize(ProgramSettings settings)
    {
        settings.SearchUrlTemplate = string.IsNullOrWhiteSpace(settings.SearchUrlTemplate)
            ? ProgramSettings.DefaultSearchUrlTemplate
            : settings.SearchUrlTemplate.Trim();

        settings.ScreencastFps = Math.Clamp(settings.ScreencastFps <= 0 ? ProgramSettings.DefaultScreencastFps : settings.ScreencastFps, 1, 60);
        settings.PluginPanelWidth = Math.Clamp(
            settings.PluginPanelWidth <= 0 ? ProgramSettings.DefaultPluginPanelWidth : settings.PluginPanelWidth,
            ProgramSettings.MinPluginPanelWidth,
            ProgramSettings.MaxPluginPanelWidth);
    }

    private static ProgramSettings Clone(ProgramSettings settings)
        => JsonSerializer.Deserialize<ProgramSettings>(JsonSerializer.Serialize(settings)) ?? new ProgramSettings();
}
