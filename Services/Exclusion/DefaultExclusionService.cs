using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Enumeration;
using Pfim;
using SkyTexOpti.POCO;
using SkyTexOpti.Services.Helpers;

namespace SkyTexOpti.Services;

public class DefaultExclusionService(
    Options options,
    ILoggingService loggingService
) : IExclusionService
{
    public async Task<ICollection<Texture>> ExcludeTextures(ICollection<Texture> textures)
    {
        var watch = new Stopwatch();
        var texturesToOptimize = new ConcurrentBag<Texture>();
        var texturesExcludedCount = 0;
        var texturesProcessedCount = 0;
        var tasks = new List<Task>();
        
        var texturesGroupedByArchive = textures
            .Where(x => x.BsaPath != null)
            .GroupBy(entry => entry.BsaPath!)
            .ToDictionary(g => g.Key, g => g.Select(kvp => kvp).ToList());

        var texturesFromLooseFiles = textures
            .Where(x => x.BsaPath == null)
            .ToList();
        
        for (var archiveIndex = 0; archiveIndex < texturesGroupedByArchive.Count; archiveIndex++)
        {
            var entry = texturesGroupedByArchive.ElementAt(archiveIndex);
            
            var bsaTask = BsaHelpers.GetBsaTask(entry.Key, entry.Value, async (stream, texture) =>
            {
                await ProcessTexture(stream, texture);
            });

            tasks.Add(bsaTask);
        }
        
        foreach (var texture in texturesFromLooseFiles)
        {
            var looseTask = Task.Run(async () =>
            {
                await using var stream = File.OpenRead(texture.TextureAbsolutePath!);
                await ProcessTexture(stream, texture);
            });

            tasks.Add(looseTask);
        }

        // Run all tasks
        watch.Start();
        await Task.WhenAll(tasks);
        watch.Stop();
        
        Console.Write($"\r{"".PadLeft(Console.CursorLeft, ' ')}");
        Console.WriteLine($"\r(100 % - {textures.Count:n0}/{textures.Count:n0} - {watch.Elapsed:c}) Excluding textures... Excluded {texturesExcludedCount:n0} textures.");

        return texturesToOptimize.ToList();

        async Task ProcessTexture(Stream stream, Texture texture)
        {
            Interlocked.Increment(ref texturesProcessedCount);
            
            if (await IsExcluded(stream, texture))
            {
                Interlocked.Increment(ref texturesExcludedCount);
            }
            else
            {
                texturesToOptimize.Add(texture);
            }
            
            Console.Write($"\r{"".PadLeft(Console.CursorLeft, ' ')}");
            Console.Write($"\r({texturesProcessedCount / (float)textures.Count:p} - {texturesProcessedCount:n0}/{textures.Count:n0} - {watch.Elapsed:c}) Excluding textures... {texture.Mod.Name} - {texture.TextureRelativePath}");
        }
    }

    private async Task<bool> IsExcluded(Stream stream, Texture texture)
    {
        if (!texture.TextureRelativePath.StartsWith("textures/") || !texture.TextureRelativePath.EndsWith(".dds"))
            return true;

        if (IsAlreadyExistant(texture))
        {
            await loggingService.WriteExclusionLog("Already exists on disk", texture);
            return true;
        }

        string? matchingPattern;
        if (IsExcludedByFilename(texture, out matchingPattern)
            || IsExludedByPath(texture, out matchingPattern)
            || IsExcludedByTarget(texture, out matchingPattern))
        {
            await loggingService.WriteExclusionLog($"Matches {matchingPattern}", texture);
            return true;
        }

        try
        {
            var headers = new DdsHeader(stream);
            texture.Height = headers.Height;
            texture.Width = headers.Width;

            if (IsExcludedByResolution(texture, out var exclusionResolutionReason))
            {
                await loggingService.WriteExclusionLog(exclusionResolutionReason!, texture);
                return true;
            }
        }
        catch (Exception e)
        {
            await loggingService.WriteErrorLog($"Unable to open {texture.TextureRelativePath}, reason : {e.Message}");
            return true;
        }

        return false;
    }

    private bool IsExcludedByFilename(Texture texture, out string? matchingPattern)
    {
        matchingPattern = null;
        foreach (var pattern in options.ExclusionsFilename)
        {
            if (FileSystemName.MatchesSimpleExpression(pattern, texture.TextureRelativePath))
            {
                matchingPattern = pattern;
                return true;
            }
        }

        return false;
    }

    private bool IsExludedByPath(Texture texture, out string? matchingPattern)
    {
        matchingPattern = null;
        foreach (var pattern in options.ExclusionsPath)
            if (new FileInfo(texture.TextureRelativePath).DirectoryName!.ToLower().Contains(pattern))
            {
                matchingPattern = pattern;
                return true;
            }

        return false;
    }

    private bool IsExcludedByTarget(Texture texture, out string? matchingTarget)
    {
        matchingTarget = options.Targets.Keys.FirstOrDefault(texture.TextureRelativePath.EndsWith);
        return matchingTarget == null;
    }

    private bool IsExcludedByResolution(Texture texture, out string? reason)
    {
        reason = null;
        var targetResolution = options.Targets.First(t => texture.TextureRelativePath.EndsWith(t.Key)).Value;

        if (texture.Height <= targetResolution || texture.Width <= targetResolution)
        {
            reason = "Too small";
            return true;
        }

        return false;
    }

    private bool IsAlreadyExistant(Texture texture)
    {
        return options.Resume && new FileInfo(Path.Combine(options.OutputPath!.FullName, texture.TextureRelativePath)).Exists;
    }
}