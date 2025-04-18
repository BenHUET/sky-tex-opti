using System.Diagnostics;
using System.IO.Enumeration;
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
    
    private static uint _targetResolutionDiffuse;
    private static uint _targetResolutionNormal;
    private static uint _targetResolutionModelSpaceNormal;
    private static uint _targetResolutionReflection;
    private static uint _targetResolutionSubsurfaceScattering;
    private static uint _targetResolutionSpecular;
    private static uint _targetResolutionGlow;
    private static uint _targetResolutionBacklighting;
    private static uint _targetResolutionEnvironmentAndCubemap;
    private static uint _targetResolutionHeight;

    private static bool _logging = true;

    private static Dictionary<TextureType, string[]> _textureTypesMap = new()
    {
        { TextureType.Diffuse, ["_d.dds"] },
        { TextureType.Normal, ["_n.dds", "_normal.dds"] },
        { TextureType.ModelSpaceNormal , ["_msn.dds"] },
        { TextureType.Reflection, ["_m.dds"] },
        { TextureType.SubsurfaceScattering, ["_sk.dds"] },
        { TextureType.Specular, ["_s.dds"] },
        { TextureType.Glow, ["_g.dds"] },
        { TextureType.Backlighting, ["_b.dds"] },
        { TextureType.EnvironmentAndCubemap, ["_e.dds"] },
        { TextureType.Height, ["_h.dds", "_p.dds"] },
    };

    private static List<string> _exclusionsFilename = new();
    private static List<string> _exclusionsPath = new();
    private static List<TextureType> _includedTypes = new();

    private static string _logfileExclusionsPath;
    private static Mutex _logfileExclusionsMutex = new();
    private static string _logfileErrorsPath;
    private static Mutex _logfileErrorsMutex = new();

    static void Main(string[] args)
    {
        Parser
            .Default
            .ParseArguments<Options>(args)
            .WithParsed(o =>
            {
                _profileFolder = o.ProfilePath;
                _outputFolder = o.OutputPath;

                _targetResolutionDiffuse = o.ResolutionDiffuse;
                _targetResolutionNormal = o.ResolutionNormal;
                _targetResolutionModelSpaceNormal = o.ResolutionModelSpaceNormal;
                _targetResolutionReflection = o.ResolutionReflection;
                _targetResolutionSubsurfaceScattering = o.ResolutionSubsurfaceScattering;
                _targetResolutionSpecular = o.ResolutionSpecular;
                _targetResolutionGlow = o.ResolutionGlow;
                _targetResolutionBacklighting = o.ResolutionBacklighting;
                _targetResolutionEnvironmentAndCubemap = o.ResolutionEnvironment;
                _targetResolutionHeight = o.Resolutionheight;

                _logging = o.LoggingEnabled;
                
                foreach (var arg in o.IncludedTypes.Split(','))
                {
                    var success = Enum.TryParse(arg.TrimEnd().TrimStart(), out TextureType type);
                    
                    if (!success)
                        throw new ArgumentException("Unknown texture type.");
                    
                    _includedTypes.Add(type);
                }

                _exclusionsFilename = o.ExcludedFilenames.Split(',').Select(x => x.Trim()).ToList();
                
                _exclusionsPath = o.ExcludedPaths
                    .Split(',')
                    .Select(x => x.Trim().ToLower().Replace('\\', '/').Replace('/', Path.DirectorySeparatorChar))
                    .ToList();

                _ = Run();
            });
    }

    private static async Task Run()
    {
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
            Console.Write($"\r({(mods.Count - modIndex) / (float)mods.Count:p} - {watch.Elapsed:c}) Discovering textures to optimize... {mods[modIndex]}");

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
                            TextureRelativePath = textureRelativePath,
                            Type = GuessTextureType(textureRelativePath)
                        };

                        using var stream = file.AsStream();
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
                    TextureAbsolutePath = looseTexture.FullName,
                    Type = GuessTextureType(textureRelativePath)
                };

                using var stream = File.OpenRead(looseTexture.FullName);
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
        Console.WriteLine(
            $"\r(100 % - {watch.Elapsed:c}) Discovering textures to optimize... Found {discoveredTextures.Count} textures ({excluded} excluded).");

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
                        Console.Write($"\r({texturesOptimized / (float)discoveredTextures.Count:p} - {watch.Elapsed:c}) Optimizing textures... {file.Path}");
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
                Console.Write($"\r({texturesOptimized / (float)discoveredTextures.Count:p} - {watch.Elapsed:c}) Optimizing textures... {texture.TextureAbsolutePath}");
            });

            textureTasks.Add(task);
        }

        watch.Restart();
        await Task.WhenAll(textureTasks);

        watch.Stop();
        Console.Write($"\r{"".PadLeft(Console.CursorLeft, ' ')}");
        Console.Write($"\r(100 % - {watch.Elapsed:c}) Optimizing textures... Done.");

        Console.WriteLine($"\n\nCreate a new mod with MO2 and move the `textures` folder inside it. Place the mod at the end of your modlist.\n\nEND. PRESS KEY TO EXIT.");
        Console.ReadKey();
    }

    static TextureType GuessTextureType(string path)
    {
        var type = TextureType.Diffuse;
        foreach (var entry in _textureTypesMap)
        {
            if (entry.Value.Any(path.EndsWith))
            {
                type = entry.Key;
                break;
            }
        }

        return type;
    }

    static bool IsExcludedByFilename(string path)
    {
        foreach (var pattern in _exclusionsFilename)
            if (FileSystemName.MatchesSimpleExpression(pattern, path))
            {
                WriteLogExclusion($"matches {pattern}\t{path}");
                return true;
            }

        foreach (var pattern in _exclusionsPath)
            if (new FileInfo(path).DirectoryName!.ToLower().Contains(pattern))
            {
                WriteLogExclusion($"matches {pattern}\t{path}");
                return true;
            }

        var textureType = GuessTextureType(path);
        if (!_includedTypes.Contains(textureType))
        {
            WriteLogExclusion($"matches type {textureType}\t{path}");
            return true;
        }

        return false;
    }

    static bool IsExcludedByHeaders(Stream stream, Texture texture)
    {
        try
        {
            var headers = new DdsHeader(stream);

            var targetResolution = GetTargetResolution(texture.Type);

            if (headers.Height <= targetResolution || headers.Width <= targetResolution)
            {
                WriteLogExclusion($"too small\t{texture.TextureRelativePath}");
                return true;
            }

            if (headers.Height >= 8192 || headers.Width >= 8192)
            {
                WriteLogExclusion($"too big\t{texture.TextureRelativePath}");
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

            var targetResolution = GetTargetResolution(texture.Type);

            float scaleFactor = 1;
            if (image.Width < image.Height)
                scaleFactor = targetResolution / (float)image.Width;
            else
                scaleFactor = targetResolution / (float)image.Height;

            if (scaleFactor == 1)
            {
                return;
            }

            image.Resize((uint)(image.Width * scaleFactor), (uint)(image.Height * scaleFactor));
            image.Write(outputPath);
        }
        catch (Exception e)
        {
            WriteLogError($"{texture.TextureRelativePath} : {e.Message}");
        }
    }

    static uint GetTargetResolution(TextureType type)
    {
        return type switch
        {
            TextureType.Diffuse => _targetResolutionDiffuse,
            TextureType.Normal => _targetResolutionNormal,
            TextureType.ModelSpaceNormal => _targetResolutionModelSpaceNormal,
            TextureType.Reflection => _targetResolutionReflection,
            TextureType.SubsurfaceScattering => _targetResolutionSubsurfaceScattering,
            TextureType.Specular => _targetResolutionSpecular,
            TextureType.Glow => _targetResolutionGlow,
            TextureType.Backlighting => _targetResolutionBacklighting,
            TextureType.EnvironmentAndCubemap => _targetResolutionEnvironmentAndCubemap,
            TextureType.Height => _targetResolutionHeight,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }

    static void WriteLogExclusion(string content) => WriteLog(_logfileExclusionsPath, _logfileExclusionsMutex, content);
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