namespace SkyTexOpti.POCO;

public class Texture
{
    public required Mod Mod { get; init; }
    public string? BsaPath { get; init; }
    public string? TextureAbsolutePath { get; init; }
    public required string TextureRelativePath { get; init; }
}