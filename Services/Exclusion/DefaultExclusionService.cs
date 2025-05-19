using System.IO.Enumeration;
using SkyTexOpti.POCO;

namespace SkyTexOpti.Services;

public class DefaultExclusionService(Options options) : IExclusionService
{
    public bool IsExcludedByFilename(Texture texture, out string? matchingPattern)
    {
        matchingPattern = null;
        foreach (var pattern in options.ExclusionsFilename)
            if (FileSystemName.MatchesSimpleExpression(pattern, texture.TextureRelativePath))
            {
                matchingPattern = pattern;
                return true;
            }

        return false;
    }

    public bool IsExludedByPath(Texture texture, out string? matchingPattern)
    {
        matchingPattern = null;
        foreach (var pattern in options.ExclusionsPath)
            if (new FileInfo(texture.TextureRelativePath).DirectoryName!.ToLower().Contains(pattern))
            {
                matchingPattern = pattern;
                return true;
            }

        return false;
    }

    public bool IsExcludedByTarget(Texture texture, out string? matchingTarget)
    {
        matchingTarget = options.Targets.Keys.FirstOrDefault(texture.TextureRelativePath.EndsWith);
        return matchingTarget == null;
    }

    public bool IsExcludedByResolution(Texture texture, out string? reason)
    {
        reason = null;
        var targetResolution = options.Targets.First(t => texture.TextureRelativePath.EndsWith(t.Key)).Value;

        if (texture.Height <= targetResolution || texture.Width <= targetResolution)
        {
            reason = "Too small";
            return true;
        }

        return false;
    }

    public bool IsAlreadyExistant(Texture texture)
    {
        return options.Resume && new FileInfo(Path.Combine(options.OutputPath!.FullName, texture.TextureRelativePath)).Exists;
    }
}