using System.Diagnostics;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Archives;
using SkyTexOpti.POCO;

namespace SkyTexOpti.Services;

public class DefaultOptimizationService(IResizerService resizerService) : IOptimizationService
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
            var bsaTask = Task.Run(async () =>
            {
                var bsaSubTasks = new List<Task>();
                var bsaReader = Archive.CreateReader(GameRelease.SkyrimSE, entry.Key);
                foreach (var file in bsaReader.Files)
                {
                    var texture = entry.Value.FirstOrDefault(t => t.TextureRelativePath == file.Path);

                    if (texture == null)
                        continue;

                    var stream = file.AsStream();

                    var task = Task.Run(() =>
                    {
                        resizerService.Resize(stream, texture);

                        Interlocked.Increment(ref texturesOptimized);
                        Console.Write($"\r{"".PadLeft(Console.CursorLeft, ' ')}");
                        Console.Write(
                            $"\r({texturesOptimized / (float)textures.Count:p} - {texturesOptimized}/{textures.Count} - {watch.Elapsed:c}) Optimizing textures... {texture.Mod.Name} - {texture.TextureRelativePath}");
                        
                        stream.Dispose();
                    });

                    bsaSubTasks.Add(task);
                }

                await Task.WhenAll(bsaSubTasks);
            });

            tasks.Add(bsaTask);
        }

        // Generate tasks for loose textures
        foreach (var texture in texturesFromLooseFiles)
        {
            var looseTask = Task.Run(() =>
            {
                using var stream = File.OpenRead(texture.TextureAbsolutePath!);
                resizerService.Resize(stream, texture);

                Interlocked.Increment(ref texturesOptimized);
                Console.Write($"\r{"".PadLeft(Console.CursorLeft, ' ')}");
                Console.Write(
                    $"\r({texturesOptimized / (float)textures.Count:p} - {texturesOptimized}/{textures.Count} - {watch.Elapsed:c}) Optimizing textures... {texture.Mod.Name} - {texture.TextureRelativePath}");
            });

            tasks.Add(looseTask);
        }

        // Run all tasks
        watch.Start();
        await Task.WhenAll(tasks);
        watch.Stop();

        Console.Write($"\r{"".PadLeft(Console.CursorLeft, ' ')}");
        Console.WriteLine($"\r(100 % - {textures.Count}/{textures.Count} - {watch.Elapsed:c}) Optimizing textures... Done.");
    }
}