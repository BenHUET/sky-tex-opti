using SkyTexOpti.POCO;

namespace SkyTexOpti.Services;

public interface IModsLoaderService
{
    public Task<IList<Mod>> GetOrderedModList();
}