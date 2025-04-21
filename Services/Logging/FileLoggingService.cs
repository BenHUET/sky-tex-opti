using SkyTexOpti.POCO;

namespace SkyTexOpti.Services;

public class LoggingService(
    Options options
) : ILoggingService
{
    public async Task WriteGeneralLog(string content, Texture texture)
    {
        await WriteLog(Path.Combine(options.OutputPath!.FullName, "main.log"), $"{content}\t\t\t\t\t\t{texture.TextureRelativePath}");
    }

    public async Task WriteExclusionLog(string reason, Texture texture)
    {
        await WriteLog(Path.Combine(options.OutputPath!.FullName, "exclusions.log"), $"{reason}\t\t\t\t\t\t{texture.TextureRelativePath}");
    }

    public async Task WriteErrorLog(string content)
    {
        await WriteLog(Path.Combine(options.OutputPath!.FullName, "errors.log"), content);
    }

    private async Task WriteLog(string path, string content)
    {
        await File.AppendAllTextAsync(path, $"\n{content}");
    }
}