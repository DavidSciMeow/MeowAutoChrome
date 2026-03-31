namespace MeowAutoChrome.Core.Interface;

/// <summary>
/// 程序设置提供者接口，负责读取、保存以及注入宿主提供的自定义设置。<br/>
/// Interface for program settings provider responsible for reading, saving and injecting host-provided custom settings.
/// </summary>
public interface IProgramSettingsProvider
{
    /// <summary>
    /// 获取当前程序设置。<br/>
    /// Get the current program settings.
    /// </summary>
    Task<Struct.ProgramSettings> GetAsync();
    /// <summary>
    /// 保存程序设置。<br/>
    /// Save program settings.
    /// </summary>
    Task SaveAsync(Struct.ProgramSettings settings);

    /// <summary>
    /// 注入/合并来自宿主（例如 Web）的自定义设置字典到现有程序设置中并保存。<br/>
    /// Inject/merge a dictionary of custom settings from the host (e.g., web) into existing program settings and save.
    /// </summary>
    /// <param name="customSettings">要注入的键值对（字符串->字符串）。<br/>Key/value pairs to inject (string->string).</param>
    Task InjectCustomSettingsAsync(IDictionary<string, string?> customSettings);
}
