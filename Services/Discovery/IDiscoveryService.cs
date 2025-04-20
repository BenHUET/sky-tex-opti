using SkyTexOpti.POCO;

namespace SkyTexOpti.Services;

public interface IDiscoveryService
{
    public Task<ICollection<Texture>> GetTextures(IList<Mod> mods);
}