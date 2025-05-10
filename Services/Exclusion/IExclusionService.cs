using SkyTexOpti.POCO;

namespace SkyTexOpti.Services;

public interface IExclusionService
{
    public bool IsExcludedByFilename(Texture texture, out string? matchingPattern);
    public bool IsExludedByPath(Texture texture, out string? matchingPattern);
    public bool IsExcludedByTarget(Texture texture, out string? matchingTarget);
    public bool IsExcludedByResolution(Texture texture, Stream stream, out string? reason);
    public bool IsAlreadyExistant(Texture texture);
}