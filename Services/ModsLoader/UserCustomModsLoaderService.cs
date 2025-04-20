using SkyTexOpti.POCO;

namespace SkyTexOpti.Services;

public class UserCustomModsLoaderService(Options options) : IModsLoaderService
{
    public async Task<IList<Mod>> GetOrderedModList()
    {
        return (await File.ReadAllLinesAsync(options.CustomModlist!.FullName))
            .Select(path => new DirectoryInfo(path))
            .Select(modPath => new Mod(modPath.Name, modPath))
            .ToList();
    }
}