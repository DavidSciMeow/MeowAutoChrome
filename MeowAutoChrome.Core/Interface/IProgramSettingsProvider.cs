namespace MeowAutoChrome.Core.Interface;

public interface IProgramSettingsProvider
{
    Task<Struct.ProgramSettings> GetAsync();
    Task SaveAsync(Struct.ProgramSettings settings);

    /// <summary>
    /// 注入/合并来自宿主（例如 Web）的自定义设置字典到现有程序设置中并保存。
    /// </summary>
    /// <param name="customSettings">要注入的键值对（字符串->字符串）。</param>
    Task InjectCustomSettingsAsync(IDictionary<string, string?> customSettings);
}
