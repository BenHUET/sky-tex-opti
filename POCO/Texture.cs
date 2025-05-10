namespace SkyTexOpti.POCO;

public class Texture
{
    public required Mod Mod { get; set; }
    public string? BsaPath { get; init; }
    public string? TextureAbsolutePath { get; init; }
    public required string TextureRelativePath { get; init; }
}