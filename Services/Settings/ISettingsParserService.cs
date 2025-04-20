using SkyTexOpti.POCO;

namespace SkyTexOpti.Services;

public interface ISettingsParserService
{
    public Task<Settings> GetSettings();
}