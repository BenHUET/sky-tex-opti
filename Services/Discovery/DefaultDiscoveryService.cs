using System.Diagnostics;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Archives;
using Pfim;
using SkyTexOpti.POCO;

namespace SkyTexOpti.Services;

public class DefaultDiscoveryService(
    IExclusionService exclusionService,
    ILoggingService loggingService
) : IDiscoveryService
{
    private readonly Dictionary<string, Texture?> _discoveredTextures = new();
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
                        Mod = mod,
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
                    Mod = mod,
                    TextureRelativePath = Path.GetRelativePath(mod.Path.FullName, looseTexture.FullName).ToLower(),
                    TextureAbsolutePath = looseTexture.FullName
                };

                await ProcessTexture(texture, () => File.OpenRead(looseTexture.FullName));
            }
        }

        watch.Stop();

        var results = _discoveredTextures
            .Values
            .Where(t => t != null)
            .Select(t => t!)
            .ToList();

        Console.Write($"\r{"".PadLeft(Console.CursorLeft, ' ')}");
        Console.WriteLine(
            $"\r(100 % - {mods.Count}/{mods.Count} - {watch.Elapsed:c}) Discovering textures to optimize... Found {results.Count:n0} textures to optimize ({_exclusionsCount:n0} excluded).");

        return results;
    }

    private async Task ProcessTexture(Texture texture, Func<Stream> getStream)
    {
        if (!texture.TextureRelativePath.StartsWith("textures/")
            || !texture.TextureRelativePath.EndsWith(".dds")
            || _discoveredTextures.ContainsKey(texture.TextureRelativePath))
            return;

        if (exclusionService.IsAlreadyExistant(texture))
        {
            _exclusionsCount++;
            await loggingService.WriteExclusionLog("Already exists on disk", texture);
            _discoveredTextures.TryAdd(texture.TextureRelativePath, null);
            return;
        }

        string? matchingPattern;
        if (exclusionService.IsExcludedByFilename(texture, out matchingPattern)
            || exclusionService.IsExludedByPath(texture, out matchingPattern)
            || exclusionService.IsExcludedByTarget(texture, out matchingPattern))
        {
            _exclusionsCount++;
            await loggingService.WriteExclusionLog($"Matches {matchingPattern}", texture);
            _discoveredTextures.TryAdd(texture.TextureRelativePath, null);
            return;
        }

        try
        {
            await using var stream = getStream();

            var headers = new DdsHeader(stream);
            texture.Height = headers.Height;
            texture.Width = headers.Width;
            
            if (exclusionService.IsExcludedByResolution(texture, out var exclusionResolutionReason))
            {
                _exclusionsCount++;
                await loggingService.WriteExclusionLog(exclusionResolutionReason!, texture);
                _discoveredTextures.TryAdd(texture.TextureRelativePath, null);
                return;
            }
        }
        catch (Exception e)
        {
            await loggingService.WriteErrorLog($"Unable to open {texture.TextureRelativePath}, reason : {e.Message}");
            _discoveredTextures.TryAdd(texture.TextureRelativePath, null);
            return;
        }

        _discoveredTextures.TryAdd(texture.TextureRelativePath, texture);
    }
}