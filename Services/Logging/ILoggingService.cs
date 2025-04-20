using SkyTexOpti.POCO;

namespace SkyTexOpti.Services;

public interface ILoggingService
{
    public Task WriteGeneralLog(string content, Texture texture);
    public Task WriteExclusionLog(string reason, Texture texture);
    public Task WriteErrorLog(string content);
}