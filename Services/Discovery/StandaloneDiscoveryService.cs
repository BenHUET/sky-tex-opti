using System.Diagnostics;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Archives;
using SkyTexOpti.POCO;

namespace SkyTexOpti.Services;

public class StandaloneDiscoveryService : IDiscoveryService
{
    public Task<ICollection<Texture>> GetTextures(IList<Mod> mods)
    {
        var textures = new Dictionary<string, Texture>();
        var watch = new Stopwatch();

        watch.Start();

        for (var modIndex = mods.Count - 1; modIndex >= 0; modIndex--)
        {
            var mod = mods[modIndex];

            Console.Write($"\r{"".PadLeft(Console.CursorLeft, ' ')}");
            Console.Write($"\r({(mods.Count - modIndex) / (float)mods.Count:p} - {mods.Count - modIndex:n0}/{mods.Count:n0} - {watch.Elapsed:c}) Discovering textures to optimize... {mods[modIndex].Name}");

            // From BSA archives
            foreach (var archive in mod.Path.EnumerateFiles("*.bsa", SearchOption.AllDirectories))
            {
                var bsaReader = Archive.CreateReader(GameRelease.SkyrimSE, archive);
                foreach (var file in bsaReader.Files)
                {
                    if (file.Path.ToLower().EndsWith(".dds"))
                    {
                        var texture = new Texture
                        {
                            Mod = mod,
                            BsaPath = archive.FullName,
                            TextureRelativePath = file.Path.ToLower()
                        };

                        textures.TryAdd(texture.TextureRelativePath, texture);
                    }
                }
            }

            // From loose files
            foreach (var looseTexture in mod.Path.EnumerateFiles("*.dds", SearchOption.AllDirectories))
            {
                var texture = new Texture
                {
                    Mod = mod,
                    TextureRelativePath = Path.GetRelativePath(mod.Path.FullName, looseTexture.FullName).ToLower(),
                    TextureAbsolutePath = looseTexture.FullName
                };

                textures.TryAdd(texture.TextureRelativePath, texture);
            }
        }

        watch.Stop();

        Console.Write($"\r{"".PadLeft(Console.CursorLeft, ' ')}");
        Console.WriteLine($"\r(100 % - {mods.Count:n0}/{mods.Count:n0} - {watch.Elapsed:c}) Discovering textures to optimize... Found {textures.Count:n0} textures.");

        return Task.FromResult<ICollection<Texture>>(textures.Select(kpv => kpv.Value).ToArray());
    }
}