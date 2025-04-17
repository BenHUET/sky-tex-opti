namespace sky_tex_opti.POCO;

public class Texture
{
    public required string ModName { get; init; }
    public string? BsaPath { get; init; }
    public string? TextureAbsolutePath { get; init; }
    public required string TextureRelativePath { get; init; }
    public TextureType Type { get; init; }
}