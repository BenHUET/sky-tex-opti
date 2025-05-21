using Mutagen.Bethesda;
using Mutagen.Bethesda.Archives;
using SkyTexOpti.POCO;

namespace SkyTexOpti.Services.Helpers;

public static class BsaHelpers
{
    public static Task GetBsaTask(string bsaPath, ICollection<Texture> textures, Func<Stream, Texture, Task> action)
    {
        return Task.Run(async () =>
        {
            var bsaSubTasks = new List<Task>();
            var bsaReader = Archive.CreateReader(GameRelease.SkyrimSE, bsaPath);
            foreach (var file in bsaReader.Files)
            {
                var texture = textures.FirstOrDefault(t => t.TextureRelativePath == file.Path);

                if (texture == null)
                    continue;

                var stream = file.AsStream();

                var task = Task.Run(async () =>
                {
                    try
                    {
                        await action.Invoke(stream, texture);
                    }
                    finally
                    {
                        await stream.DisposeAsync();
                    }
                });

                bsaSubTasks.Add(task);
            }

            await Task.WhenAll(bsaSubTasks);
        });
    }
}