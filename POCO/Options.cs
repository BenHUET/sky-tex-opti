using CommandLine;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SkyTexOpti.POCO;

public class Options
{
    [Option("profile", Required = true, HelpText = "Path to the MO2 profile to optimize.", SetName = "mod-organizer")]
    public DirectoryInfo? Mo2ProfilePath { get; init; }

    [Option("modlist", Required = true, HelpText = "Path to a custom modlist : newline-separated list of absolute paths to mods.", SetName = "custom-modlist")]
    public FileInfo? CustomModlist { get; init; }

    [Option("output", Required = true, HelpText = "Path where to store the output.")]
    public DirectoryInfo? OutputPath { get; init; }

    [Option("settings", Default = "default.json", HelpText = "Path to the settings file.")]
    public string? SettingsPath { get; init; }
    
    [Option("resume", Default = false, HelpText = "Will use an existing output folder and process only missing files.")]
    public bool Resume { get; init; }

    public Dictionary<string, uint> Targets { get; set; } = new();
    public List<string> ExclusionsFilename { get; set; } = [];
    public List<string> ExclusionsPath { get; set; } = [];
}