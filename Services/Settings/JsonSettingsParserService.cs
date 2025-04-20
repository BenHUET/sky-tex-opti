using System.Text.Json;
using SkyTexOpti.POCO;

namespace SkyTexOpti.Services;

public class JsonSettingsParserService(Options options) : ISettingsParserService
{
    public async Task<Settings> GetSettings()
    {
        var json = new FileStream(options.SettingsPath!, FileMode.Open);
        return await JsonSerializer.DeserializeAsync<Settings>(
            json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            },
            CancellationToken.None
        ) ?? throw new InvalidOperationException();
    }
}