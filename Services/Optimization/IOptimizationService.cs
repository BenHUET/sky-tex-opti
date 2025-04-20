using SkyTexOpti.POCO;

namespace SkyTexOpti.Services;

public interface IOptimizationService
{
    public Task OptimizeTextures(ICollection<Texture> textures);
}