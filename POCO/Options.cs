using CommandLine;

namespace sky_tex_opti.POCO;

public class Options
{
    [Option("profile", Required = true, HelpText = "Path to the MO2 profile to optimize.")]
    public DirectoryInfo ProfilePath { get; set; }
    
    [Option("output", Required = true, HelpText = "Path where to store the output.")]
    public DirectoryInfo OutputPath { get; set; }
    
    [Option("diffuse", Default = (uint)2048, HelpText = "Target resolution for diffuse textures.")]
    public uint ResolutionDiffuse { get; set; }
    
    [Option("normal", Default = (uint)1024, HelpText = "Target resolution for normal textures.")]
    public uint ResolutionNormal { get; set; }
    
    [Option("model-space-normal", Default = (uint)1024, HelpText = "Target resolution for model space normal textures.")]
    public uint ResolutionModelSpaceNormal { get; set; }
    
    [Option("reflection", Default = (uint)1024, HelpText = "Target resolution for reflection textures.")]
    public uint ResolutionReflection { get; set; }
    
    [Option("subsurface-scattering", Default = (uint)1024, HelpText = "Target resolution for subsurface scattering textures.")]
    public uint ResolutionSubsurfaceScattering { get; set; }
    
    [Option("specular", Default = (uint)1024, HelpText = "Target resolution for specular textures.")]
    public uint ResolutionSpecular { get; set; }
    
    [Option("glow", Default = (uint)1024, HelpText = "Target resolution for glow textures.")]
    public uint ResolutionGlow { get; set; }
    
    [Option("backlighting", Default = (uint)1024, HelpText = "Target resolution for backlighting textures.")]
    public uint ResolutionBacklighting { get; set; }
    
    [Option("environment", Default = (uint)1024, HelpText = "Target resolution for environment and cubemap textures.")]
    public uint ResolutionEnvironment { get; set; }
    
    [Option("height", Default = (uint)1024, HelpText = "Target resolution for height and parallax textures.")]
    public uint Resolutionheight { get; set; }
    
    [Option("logging", Default = true, HelpText = "Write log files.")]
    public bool LoggingEnabled { get; set; }
    
    [Option("included-types", Default = "Diffuse,Normal,Height,EnvironmentAndCubemap", HelpText = "Comma-separated list of texture types to include. Should match the TextureType enum.")]
    public string IncludedTypes { get; set; }
    
    [Option("excluded-filenames", Default = "*_color.dds,*_emissive.dds,*_OpenGL.dds,icewall*.dds,*DrJ*.dds,*envmask.dds,*_s.dds,*_a.dds,icewall*.dds,*drj*.dds,*pot_n.dds,*tg_field_rocks.dds,*tg_field_rocks_n.dds,*tg_snow_pebbles.dds,*tg_snow_pebbles_n.dds,*clgorehowl*.dds,*woodcut.dds,*woodcut_n.dds,*dummy.dds,*lod*_p.dds,*default_n.dds,*basket01.dds", HelpText = "Comma-separated list of pattern to exclude.")]
    public string ExcludedFilenames { get; set; }
    
    [Option("excluded-paths", Default = "/interface,/effects03/newmiller/jewels2,/littlebaron,/luxonbeacon,/landscape/mountains,/landscape/rocks,/terrain,/lod,/alduin,/dragon,/durnehviir,/odahviing,/paarthurnax,/actors/dragon,/actors/alduin,/dlc01/actors/undeaddragon,/dyndolod,/lodgen,!_rudy_misc,!sr,!!sr", HelpText = "Comma-separated list of paths to exclude.")]
    public string ExcludedPaths { get; set; }
}