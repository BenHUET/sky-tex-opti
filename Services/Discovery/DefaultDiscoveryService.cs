using System.Diagnostics;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Archives;
using SkyTexOpti.POCO;

namespace SkyTexOpti.Services;

public class DefaultDiscoveryService(
    IExclusionService exclusionService,
    ILoggingService loggingService
) : IDiscoveryService
{
    private readonly Dictionary<string, Texture> _discoveredTextures = new();
    private int _exclusionsCount;

    public async Task<ICollection<Texture>> GetTextures(IList<Mod> mods)
    {
        var watch = new Stopwatch();

        watch.Start();

        for (var modIndex = mods.Count - 1; modIndex >= 0; modIndex--)
        {
            var mod = mods[modIndex];

            Console.Write($"\r{"".PadLeft(Console.CursorLeft, ' ')}");
            Console.Write(
                $"\r({(mods.Count - modIndex) / (float)mods.Count:p} - {mods.Count - modIndex}/{mods.Count} - {watch.Elapsed:c}) Discovering textures to optimize... {mods[modIndex].Name}");

            // From BSA archives
            foreach (var archive in mod.Path.EnumerateFiles("*.bsa", SearchOption.AllDirectories))
            {
                var bsaReader = Archive.CreateReader(GameRelease.SkyrimSE, archive);
                foreach (var file in bsaReader.Files)
                {
                    var texture = new Texture
                    {
                        BsaPath = archive.FullName,
                        TextureRelativePath = file.Path.ToLower()
                    };

                    await ProcessTexture(texture, () => file.AsStream());
                }
            }

            // From loose files
            foreach (var looseTexture in mod.Path.EnumerateFiles("*.dds", SearchOption.AllDirectories))
            {
                var texture = new Texture
                {
                    TextureRelativePath = Path.GetRelativePath(mod.Path.FullName, looseTexture.FullName).ToLower(),
                    TextureAbsolutePath = looseTexture.FullName
                };

                await ProcessTexture(texture, () => File.OpenRead(looseTexture.FullName));
            }
        }

        watch.Stop();

        Console.Write($"\r{"".PadLeft(Console.CursorLeft, ' ')}");
        Console.WriteLine(
            $"\r(100 % - {mods.Count}/{mods.Count} - {watch.Elapsed:c}) Discovering textures to optimize... Found {_discoveredTextures.Count:n0} textures to optimize ({_exclusionsCount:n0} excluded).");

        return _discoveredTextures
            .Select(e => e.Value)
            .ToList();
    }

    private async Task ProcessTexture(Texture texture, Func<Stream> getStream)
    {
        if (!texture.TextureRelativePath.StartsWith("textures/") || !texture.TextureRelativePath.EndsWith(".dds"))
            return;

        if (_discoveredTextures.ContainsKey(texture.TextureRelativePath))
            return;

        if (exclusionService.IsExcludedByFilename(texture, out var matchingFilenamePattern))
        {
            _exclusionsCount++;
            await loggingService.WriteExclusionLog($"Matches {matchingFilenamePattern}", texture);
            return;
        }

        if (exclusionService.IsExludedByPath(texture, out var matchingPathPattern))
        {
            _exclusionsCount++;
            await loggingService.WriteExclusionLog($"Matches {matchingPathPattern}", texture);
            return;
        }

        if (exclusionService.IsExcludedByTarget(texture, out var matchingTarget))
        {
            _exclusionsCount++;
            await loggingService.WriteExclusionLog($"Matches {matchingTarget}", texture);
            return;
        }

        await using var stream = getStream();
        try
        {
            if (exclusionService.IsExcludedByResolution(texture, stream, out var exclusionResolutionReason))
            {
                _exclusionsCount++;
                await loggingService.WriteExclusionLog(exclusionResolutionReason!, texture);
                return;
            }
        }
        catch (Exception e)
        {
            await loggingService.WriteErrorLog($"Unable to open {texture.TextureRelativePath}, reason : {e.Message}");
            return;
        }

        _discoveredTextures.TryAdd(texture.TextureRelativePath, texture);
    }
}