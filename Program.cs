using CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SkyTexOpti.POCO;

namespace SkyTexOpti.Services;

public static class Program
{
    private static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder();

        await Parser
            .Default
            .ParseArguments<Options>(args)
            .WithParsedAsync(o =>
            {
                builder.Services.AddSingleton<ILoggingService, LoggingService>();
                builder.Services.AddSingleton<Options>(_ => o);
                builder.Services.AddTransient<ISettingsParserService, JsonSettingsParserService>();
                builder.Services.AddTransient<IDiscoveryService, DefaultDiscoveryService>();
                builder.Services.AddTransient<IExclusionService, DefaultExclusionService>();
                builder.Services.AddTransient<IResizerService, ImageMagickResizerService>();
                builder.Services.AddTransient<IOptimizationService, DefaultOptimizationService>();

                if (o.CustomModlist != null)
                    builder.Services.AddTransient<IModsLoaderService, UserCustomModsLoaderService>();
                else
                    builder.Services.AddTransient<IModsLoaderService, Mo2ModsLoaderService>();

                var app = builder.Build();

                var options = app.Services.GetRequiredService<Options>();
                var settingsParserService = app.Services.GetRequiredService<ISettingsParserService>();
                var modsLoaderService = app.Services.GetRequiredService<IModsLoaderService>();
                var discoveryService = app.Services.GetRequiredService<IDiscoveryService>();
                var optimizationService = app.Services.GetRequiredService<IOptimizationService>();

                return Run(options, settingsParserService, modsLoaderService, discoveryService, optimizationService);
            });
    }

    private static async Task Run(
        Options options,
        ISettingsParserService settingsParserService,
        IModsLoaderService modsLoaderService,
        IDiscoveryService discoveryService,
        IOptimizationService optimizationService
    )
    {
        // Check output folder
        if (options.OutputPath!.Exists && Directory.EnumerateFileSystemEntries(options.OutputPath.FullName).Any() && !options.Resume)
        {
            Console.WriteLine("Output folder already exsists and is not empty. Please pick another path.");
            Console.ReadKey();
            return;
        }

        options.OutputPath.Create();

        // Parse settings
        try
        {
            var settings = await settingsParserService.GetSettings();
            options.ExclusionsFilename = settings.Exclusions.Filenames.ToList();
            options.ExclusionsPath = settings.Exclusions.Paths.ToList();
            foreach (var target in settings.Targets)
            foreach (var suffix in target.Suffixes)
            {
                options.Targets[suffix] = (uint)target.Resolution;
                options.ExclusionsFilename.Remove($"*{suffix}");
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error while parsing settings, reason : {e.Message}");
            return;
        }

        // Getting mods
        IList<Mod> mods;
        try
        {
            Console.Write("Getting mods... ");
            mods = await modsLoaderService.GetOrderedModList();
            Console.WriteLine($"Found {mods.Count} enabled.");
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error while getting mods, reason : {e.Message}");
            return;
        }

        // Discover textures
        var textures = await discoveryService.GetTextures(mods);

        // Optimize textures
        await optimizationService.OptimizeTextures(textures);
    }
}