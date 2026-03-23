namespace MeowAutoChrome.Core;

public interface IProgramSettingsProvider
{
    Task<ProgramSettings> GetAsync();
    Task SaveAsync(ProgramSettings settings);
}
