using System.Diagnostics;
using System.IO.Enumeration;
using System.Text.Json;
using CommandLine;
using ImageMagick;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Archives;
using Pfim;
using sky_tex_opti.POCO;

namespace sky_tex_opti;

class Program
{
    private static DirectoryInfo _profileFolder;
    private static DirectoryInfo _outputFolder;

    private static bool _logging = true;

    private static Dictionary<string, uint> _targets = new ();
    private static string[] _exclusionsFilename;
    private static string[] _exclusionsPath;

    private static string _logfileOutputPath;
    private static Mutex _logfileOutputMutex = new();
    private static string _logfileExclusionsPath;
    private static Mutex _logfileExclusionsMutex = new();
    private static string _logfileErrorsPath;
    private static Mutex _logfileErrorsMutex = new();

    static async Task Main(string[] args)
    {
        await Parser
            .Default
            .ParseArguments<Options>(args)
            .WithParsedAsync(o =>
            {
                _profileFolder = o.ProfilePath;
                _outputFolder = o.OutputPath;

                _logging = o.LoggingEnabled;

                var json = File.ReadAllText(o.SettingsPath);
                var settings = JsonSerializer.Deserialize<Settings>(
                    json,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        ReadCommentHandling = JsonCommentHandling.Skip
                    }
                );

                if (settings == null)
                    throw new FormatException("Unable to parse JSON settings file.");

                foreach (var target in settings.Targets)
                {
                    foreach (var suffix in target.Suffixes)
                    {
                        _targets[suffix] = (uint)target.Resolution;
                    }
                }
                
                _exclusionsFilename = settings.Exclusions.Filenames;
                _exclusionsPath = settings.Exclusions.Paths;

                return Run();
            });
    }

    private static async Task Run()
    {
        _logfileOutputPath = Path.Combine(_outputFolder.FullName, "output.log");
        _logfileExclusionsPath = Path.Combine(_outputFolder.FullName, "exclusions.log");
        _logfileErrorsPath = Path.Combine(_outputFolder.FullName, "errors.log");
        
        var modOrganizerFolder = new DirectoryInfo(_profileFolder.Parent!.Parent!.FullName);
        var modsFolder = new DirectoryInfo(Path.Combine(modOrganizerFolder.FullName, "mods"));

        var watch = new Stopwatch();

        if (!_profileFolder.Exists || !new FileInfo(Path.Combine(_profileFolder.FullName, "modlist.txt")).Exists)
        {
            Console.WriteLine("Profile folder doesn't have a modlist.txt. Verify your path.");
            Console.ReadKey();
            return;
        }

        if (_outputFolder.Exists && Directory.EnumerateFileSystemEntries(_outputFolder.FullName).Any())
        {
            Console.WriteLine("Output folder already exsists and is not empty. Please pick another path.");
            Console.ReadKey();
            return;
        }

        _outputFolder.Create();

        Console.WriteLine($"Profile folder : {_profileFolder}");

        // Getting all enabled mods
        Console.Write("Getting mods... ");

        var mods = File.ReadAllLines(Path.Combine(_profileFolder.FullName, "modlist.txt"))
            .Skip(1)
            .Where(m => m.StartsWith('+'))
            .Select(m => m.Remove(0, 1))
            .ToList();

        Console.WriteLine($"Found {mods.Count} enabled.");

        // Discovering textures
        var discoveredTextures = new Dictionary<string, Texture>();
        var excluded = 0;

        watch.Start();

        for (var modIndex = mods.Count - 1; modIndex > 0; modIndex--)
        {
            var modFolder = new DirectoryInfo(Path.Combine(modsFolder.FullName, mods[modIndex]));

            Console.Write($"\r{"".PadLeft(Console.CursorLeft, ' ')}");
            Console.Write($"\r({(mods.Count - modIndex) / (float)mods.Count:p} - {mods.Count - modIndex}/{mods.Count} - {watch.Elapsed:c}) Discovering textures to optimize... {mods[modIndex]}");

            // From BSA archives
            foreach (var archive in modFolder.EnumerateFiles("*.bsa", SearchOption.AllDirectories))
            {
                var bsaReader = Archive.CreateReader(GameRelease.SkyrimSE, archive);
                foreach (var file in bsaReader.Files)
                {
                    var textureRelativePath = file.Path.ToLower();
                    
                    if (file.Path.StartsWith("textures/") && textureRelativePath.EndsWith(".dds"))
                    {
                        if (discoveredTextures.ContainsKey(textureRelativePath))
                        {
                            continue;
                        }

                        if (IsExcludedByFilename(textureRelativePath))
                        {
                            excluded++;
                            continue;
                        }

                        var texture = new Texture
                        {
                            ModName = mods[modIndex],
                            BsaPath = archive.FullName,
                            TextureRelativePath = textureRelativePath
                        };

                        await using var stream = file.AsStream();
                        if (IsExcludedByHeaders(stream, texture))
                        {
                            excluded++;
                            continue;
                        }

                        discoveredTextures.TryAdd(texture.TextureRelativePath, texture);
                    }
                }
            }

            // From loose files
            foreach (var looseTexture in modFolder.EnumerateFiles("*.dds", SearchOption.AllDirectories))
            {
                var textureRelativePath = Path.GetRelativePath(Path.Combine(modsFolder.FullName, mods[modIndex]), looseTexture.FullName).ToLower();

                if (!textureRelativePath.StartsWith("textures/"))
                {
                    continue;
                }

                if (discoveredTextures.ContainsKey(textureRelativePath))
                {
                    continue;
                }

                if (IsExcludedByFilename(textureRelativePath))
                {
                    excluded++;
                    continue;
                }

                var texture = new Texture
                {
                    ModName = mods[modIndex],
                    TextureRelativePath = textureRelativePath,
                    TextureAbsolutePath = looseTexture.FullName
                };

                await using var stream = File.OpenRead(looseTexture.FullName);
                if (IsExcludedByHeaders(stream, texture))
                {
                    excluded++;
                    continue;
                }

                discoveredTextures.TryAdd(texture.TextureRelativePath, texture);
            }
        }

        watch.Stop();
        Console.Write($"\r{"".PadLeft(Console.CursorLeft, ' ')}");
        Console.WriteLine($"\r(100 % - {mods.Count}/{mods.Count} - {watch.Elapsed:c}) Discovering textures to optimize... Found {discoveredTextures.Count} textures to optimize ({excluded} excluded).");

        // Optimization
        var texturesGroupedByArchive = discoveredTextures
            .Where(x => x.Value.BsaPath != null)
            .GroupBy(entry => entry.Value.BsaPath!)
            .ToDictionary(g => g.Key, g => g.Select(kvp => kvp.Value).ToList());

        var texturesFromLooseFiles = discoveredTextures
            .Where(x => x.Value.BsaPath == null)
            .Select(x => x.Value)
            .ToList();

        var texturesOptimized = 0;
        var textureTasks = new List<Task>();

        for (var archiveIndex = 0; archiveIndex < texturesGroupedByArchive.Count; archiveIndex++)
        {
            var entry = texturesGroupedByArchive.ElementAt(archiveIndex);

            var bsaReader = Archive.CreateReader(GameRelease.SkyrimSE, entry.Key);
            foreach (var file in bsaReader.Files)
            {
                var texture = entry.Value.FirstOrDefault(t => t.TextureRelativePath == file.Path);
                if (texture != null)
                {
                    var stream = file.AsStream();

                    var task = Task.Run(() =>
                    {
                        OptimizeTexture(stream, texture);

                        Interlocked.Increment(ref texturesOptimized);
                        Console.Write($"\r{"".PadLeft(Console.CursorLeft, ' ')}");
                        Console.Write($"\r({texturesOptimized / (float)discoveredTextures.Count:p} - {texturesOptimized}/{discoveredTextures.Count} - {watch.Elapsed:c}) Optimizing textures... {file.Path}");
                    });

                    textureTasks.Add(task);
                }
            }
        }

        foreach (var texture in texturesFromLooseFiles)
        {
            var task = Task.Run(() =>
            {
                var stream = File.OpenRead(texture.TextureAbsolutePath!);
                OptimizeTexture(stream, texture);

                Interlocked.Increment(ref texturesOptimized);
                Console.Write($"\r{"".PadLeft(Console.CursorLeft, ' ')}");
                Console.Write($"\r({texturesOptimized / (float)discoveredTextures.Count:p} - {texturesOptimized}/{discoveredTextures.Count} - {watch.Elapsed:c}) Optimizing textures... {texture.TextureAbsolutePath}");
            });

            textureTasks.Add(task);
        }

        watch.Restart();
        await Task.WhenAll(textureTasks);

        watch.Stop();
        Console.Write($"\r{"".PadLeft(Console.CursorLeft, ' ')}");
        Console.Write($"\r(100 % - {texturesOptimized}/{discoveredTextures.Count} - {watch.Elapsed:c}) Optimizing textures... Done.");

        Console.WriteLine($"\n\nCreate a new mod with MO2 and move the `textures` folder inside it. Place the mod at the end of your modlist.\n\nEND. PRESS KEY TO EXIT.");
        Console.ReadKey();
    }

    static bool IsExcludedByFilename(string path)
    {
        foreach (var pattern in _exclusionsFilename)
            if (FileSystemName.MatchesSimpleExpression(pattern, path))
            {
                WriteLogExclusion($"matches {pattern}", path);
                return true;
            }

        foreach (var pattern in _exclusionsPath)
            if (new FileInfo(path).DirectoryName!.ToLower().Contains(pattern))
            {
                WriteLogExclusion($"matches {pattern}", path);
                return true;
            }

        if (!_targets.Keys.Any(path.EndsWith))
        {
            WriteLogExclusion($"not targeted", path);
            return true;
        }

        return false;
    }

    static bool IsExcludedByHeaders(Stream stream, Texture texture)
    {
        try
        {
            var headers = new DdsHeader(stream);

            var targetResolution = GetTargetResolution(texture);

            if (headers.Height <= targetResolution || headers.Width <= targetResolution)
            {
                WriteLogExclusion($"too small", texture.TextureRelativePath);
                return true;
            }

            if (headers.Height >= 8192 || headers.Width >= 8192)
            {
                WriteLogExclusion($"too big", texture.TextureRelativePath);
                return true;
            }
        }
        catch (Exception e)
        {
            WriteLogError($"{texture.TextureRelativePath} : {e.Message}");
            return true;
        }

        return false;
    }

    static void OptimizeTexture(Stream stream, Texture texture)
    {
        var outputPath = Path.Combine(_outputFolder.FullName, texture.TextureRelativePath);
        new DirectoryInfo(outputPath).Parent!.Create();

        try
        {
            using var image = new MagickImage(stream);

            var targetResolution = GetTargetResolution(texture);
            var initialResolution = PrettyResolution(image.Width, image.Height);

            float scaleFactor;
            if (image.Width < image.Height)
                scaleFactor = targetResolution / (float)image.Width;
            else
                scaleFactor = targetResolution / (float)image.Height;

            image.Resize((uint)(image.Width * scaleFactor), (uint)(image.Height * scaleFactor));
            image.Write(outputPath);

            stream.Close();
            
            WriteLogOutput($"from {initialResolution} to {PrettyResolution(image.Width, image.Height)} (x{scaleFactor})", texture.TextureRelativePath);
        }
        catch (Exception e)
        {
            WriteLogError($"{texture.TextureRelativePath} : {e.Message}");
        }

        string PrettyResolution(uint width, uint height) => $"{width}x{height}";
    }

    static uint GetTargetResolution(Texture texture)
    {
        return _targets.First(t => texture.TextureRelativePath.EndsWith(t.Key)).Value;
    }

    static void WriteLogOutput(string result, string texturePath) => WriteLog(_logfileOutputPath, _logfileOutputMutex, $"{result}\t\t\t\t\t\t{texturePath}");
    static void WriteLogExclusion(string reason, string texturePath) => WriteLog(_logfileExclusionsPath, _logfileExclusionsMutex, $"{reason}\t\t\t\t\t\t{texturePath}");
    static void WriteLogError(string content) => WriteLog(_logfileErrorsPath, _logfileErrorsMutex, content);

    static void WriteLog(string path, Mutex mutex, string content)
    {
        if (!_logging)
            return;
        
        mutex.WaitOne();
        try
        {
            File.AppendAllText(path, $"\n{content}");
        }
        finally
        {
            mutex.ReleaseMutex();
        }
    }
}