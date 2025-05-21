using System.Diagnostics;
using SkyTexOpti.POCO;
using SkyTexOpti.Services.Helpers;

namespace SkyTexOpti.Services;

public class DefaultOptimizationService(
    Options options,
    IResizerService resizerService,
    ILoggingService loggingService) : IOptimizationService
{
    public async Task OptimizeTextures(ICollection<Texture> textures)
    {
        var watch = new Stopwatch();

        var texturesGroupedByArchive = textures
            .Where(x => x.BsaPath != null)
            .GroupBy(entry => entry.BsaPath!)
            .ToDictionary(g => g.Key, g => g.Select(kvp => kvp).ToList());

        var texturesFromLooseFiles = textures
            .Where(x => x.BsaPath == null)
            .ToList();

        var texturesOptimized = 0;
        var tasks = new List<Task>();

        // Generate a task for each BSA, each task has as many subtasks as there is textures to optimize from this BSA
        for (var archiveIndex = 0; archiveIndex < texturesGroupedByArchive.Count; archiveIndex++)
        {
            var entry = texturesGroupedByArchive.ElementAt(archiveIndex);

            var bsaTask = BsaHelpers.GetBsaTask(entry.Key, textures, async (stream, texture) =>
            {
                await ResizeTexture(stream, texture);
            });

            tasks.Add(bsaTask);
        }

        // Generate tasks for loose textures
        foreach (var texture in texturesFromLooseFiles)
        {
            var looseTask = Task.Run(async () =>
            {
                await using var stream = File.OpenRead(texture.TextureAbsolutePath!);
                await ResizeTexture(stream, texture);
            });

            tasks.Add(looseTask);
        }

        // Run all tasks
        watch.Start();
        await Task.WhenAll(tasks);
        watch.Stop();

        Console.Write($"\r{"".PadLeft(Console.CursorLeft, ' ')}");
        Console.WriteLine($"\r(100 % - {textures.Count:n0}/{textures.Count:n0} - {watch.Elapsed:c}) Optimizing textures... Done.");
        
        return;
        
        async Task ResizeTexture(Stream stream, Texture texture)
        {
            try
            {
                var targetResolution = options.Targets.First(t => texture.TextureRelativePath.EndsWith(t.Key)).Value;
                
                float scaleFactor;
                if (texture.Width < texture.Height)
                    scaleFactor = targetResolution / (float)texture.Width!;
                else
                    scaleFactor = targetResolution / (float)texture.Height!;

                var outputPath = Path.Combine(options.OutputPath!.FullName, texture.TextureRelativePath);
                new DirectoryInfo(outputPath).Parent!.Create();
                
                await resizerService.Resize(stream, texture, outputPath, scaleFactor);
                
                await loggingService.WriteGeneralLog($"From {texture.Width}x{texture.Height} to {(int)(texture.Width! * scaleFactor)}x{(int)(texture.Height! * scaleFactor)} (x{scaleFactor})", texture);
                
                Interlocked.Increment(ref texturesOptimized);
                
                Console.Write($"\r{"".PadLeft(Console.CursorLeft, ' ')}");
                Console.Write($"\r({texturesOptimized / (float)textures.Count:p} - {texturesOptimized:n0}/{textures.Count:n0} - {watch.Elapsed:c}) Optimizing textures... {texture.Mod.Name} - {texture.TextureRelativePath}");
            }
            catch (Exception e)
            {
                await loggingService.WriteErrorLog($"Failed to resize {texture.TextureRelativePath}, reason : {e.Message}");
            }
            finally
            {
                await stream.DisposeAsync();
            }
        }
    }
}