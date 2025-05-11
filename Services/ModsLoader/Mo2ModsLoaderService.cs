using SkyTexOpti.POCO;

namespace SkyTexOpti.Services;

public class Mo2ModsLoaderService(Options options) : IModsLoaderService
{
    public async Task<IList<Mod>> GetOrderedModList()
    {
        var modsFolder = new DirectoryInfo(options.Mo2ProfilePath!.Parent!.Parent!.FullName);
        return (await File.ReadAllLinesAsync(Path.Combine(options.Mo2ProfilePath.FullName, "modlist.txt")))
            .Skip(1)
            .Where(m => m.StartsWith('+'))
            .Select(m => m.Remove(0, 1))
            .Select(m => new Mod(m, new DirectoryInfo(Path.Combine(modsFolder.FullName, "mods", m))))
            .Reverse()
            .ToList();
    }
}