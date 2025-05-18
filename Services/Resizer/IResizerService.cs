using SkyTexOpti.POCO;

namespace SkyTexOpti.Services;

public interface IResizerService
{
    public Task Resize(Stream stream, Texture texture, string outputPath, float scaleFactor);
}