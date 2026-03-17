using System.Text.Json;
using MeowAutoChrome.Web.ProgarmControl;

namespace MeowAutoChrome.Web.Services;

/// <summary>
/// 程序设置服务：负责读取、规范化、保存程序配置（ProgramSettings），并在需要时迁移旧版设置文件。
/// 通过文件系统持久化配置并在内存中缓存以减少磁盘 I/O。线程安全。
/// </summary>
/// <param name="environment">ASP.NET 主机环境（用于解析路径等）。</param>
public sealed class ProgramSettingsService(IWebHostEnvironment environment)
{
    /// <summary>
    /// 同步访问设置文件的信号量，确保同一时间只有一个线程可以读取或写入配置文件，避免竞争条件和数据损坏。
    /// </summary>
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    /// <summary>
    /// appsettings.json 文件的完整路径，用于存储当前的程序设置。
    /// </summary>
    private readonly string _settingsFilePath = ProgramSettings.GetSettingsFilePath();
    /// <summary>
    /// appsettings.legacy.json 文件的完整路径，用于存储旧版本的程序设置；如果存在但新版设置文件不存在，则会自动迁移到新版位置。
    /// </summary>
    private readonly string _legacySettingsFilePath = ProgramSettings.GetLegacySettingsFilePath();
    /// <summary>
    /// appsettings.json 文件的内存缓存，避免频繁磁盘访问；在 GetAsync 和 SaveAsync 中更新和使用。仅在持有 _semaphore 时访问。
    /// </summary>
    private ProgramSettings? _cachedSettings;

    /// <summary>
    /// ASP.NET 主机环境（用于解析路径等）。
    /// </summary>
    public IWebHostEnvironment Environment { get; } = environment;

    /// <summary>
    /// 异步获取当前的程序设置副本；若配置文件不存在则返回默认设置。
    /// </summary>
    public async Task<ProgramSettings> GetAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            EnsureSettingsFileMigrated();

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

    /// <summary>
    /// 异步保存程序设置到磁盘，并更新内存缓存。
    /// </summary>
    /// <param name="settings">要保存的设置对象。</param>
    public async Task SaveAsync(ProgramSettings settings)
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
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// 规范化设置对象（填充默认值、修正不合法输入）。
    /// </summary>
    private static void Normalize(ProgramSettings settings)
    {
        settings.SearchUrlTemplate = string.IsNullOrWhiteSpace(settings.SearchUrlTemplate)
            ? ProgramSettings.DefaultSearchUrlTemplate
            : settings.SearchUrlTemplate.Trim();

        settings.UserDataDirectory = NormalizeUserDataDirectory(settings.UserDataDirectory);
        settings.UserAgent = string.IsNullOrWhiteSpace(settings.UserAgent)
            ? null
            : settings.UserAgent.Trim();

        settings.ScreencastFps = Math.Clamp(settings.ScreencastFps <= 0 ? ProgramSettings.DefaultScreencastFps : settings.ScreencastFps, 1, 60);
        settings.PluginPanelWidth = Math.Clamp(
            settings.PluginPanelWidth <= 0 ? ProgramSettings.DefaultPluginPanelWidth : settings.PluginPanelWidth,
            ProgramSettings.MinPluginPanelWidth,
            ProgramSettings.MaxPluginPanelWidth);
    }

    /// <summary>
    /// 将用户数据目录路径规范化为绝对路径并返回。
    /// </summary>
    private static string NormalizeUserDataDirectory(string? path)
    {
        var candidate = string.IsNullOrWhiteSpace(path)
            ? ProgramSettings.GetDefaultUserDataDirectoryPath()
            : path.Trim();

        return Path.GetFullPath(candidate);
    }

    /// <summary>
    /// 如果存在旧版设置文件但新版设置不存在，则将旧文件迁移到新版位置。
    /// </summary>
    private void EnsureSettingsFileMigrated()
    {
        if (File.Exists(_settingsFilePath) || !File.Exists(_legacySettingsFilePath))
            return;

        Directory.CreateDirectory(Path.GetDirectoryName(_settingsFilePath)!);
        File.Move(_legacySettingsFilePath, _settingsFilePath);
    }

    /// <summary>
    /// 深拷贝设置对象（通过序列化/反序列化实现）。
    /// </summary>
    private static ProgramSettings Clone(ProgramSettings settings)
        => JsonSerializer.Deserialize<ProgramSettings>(JsonSerializer.Serialize(settings)) ?? new ProgramSettings();
}
