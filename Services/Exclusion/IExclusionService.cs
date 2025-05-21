using System.Collections;
using SkyTexOpti.POCO;

namespace SkyTexOpti.Services;

public interface IExclusionService
{
    public Task<ICollection<Texture>> ExcludeTextures(ICollection<Texture> textures);
}