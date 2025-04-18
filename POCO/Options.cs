using CommandLine;

namespace sky_tex_opti.POCO;

public class Options
{
    public Options(DirectoryInfo profilePath, DirectoryInfo outputPath, string settingsPath, bool loggingEnabled)
    {
        ProfilePath = profilePath;
        OutputPath = outputPath;
        SettingsPath = settingsPath;
        LoggingEnabled = loggingEnabled;
    }

    [Option("profile", Required = true, HelpText = "Path to the MO2 profile to optimize.")]
    public DirectoryInfo ProfilePath { get; }
    
    [Option("output", Required = true, HelpText = "Path where to store the output.")]
    public DirectoryInfo OutputPath { get; }
    
    [Option("settings", Default = "default.json", HelpText = "Path to the settings file.")]
    public string SettingsPath { get; }
    
    [Option("logging", Default = true, HelpText = "Write log files.")]
    public bool LoggingEnabled { get; }
}