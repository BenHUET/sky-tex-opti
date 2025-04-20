using ImageMagick;
using SkyTexOpti.POCO;

namespace SkyTexOpti.Services;

public class ImageMagickResizerService(
    Options options,
    ILoggingService loggingService
) : IResizerService
{
    public async Task Resize(Stream stream, Texture texture)
    {
        var outputPath = Path.Combine(options.OutputPath!.FullName, texture.TextureRelativePath);
        new DirectoryInfo(outputPath).Parent!.Create();

        using var image = new MagickImage(stream);

        var targetResolution = options.Targets.First(t => texture.TextureRelativePath.EndsWith(t.Key)).Value;
        var initialResolution = PrettyResolution(image.Width, image.Height);

        float scaleFactor;
        if (image.Width < image.Height)
            scaleFactor = targetResolution / (float)image.Width;
        else
            scaleFactor = targetResolution / (float)image.Height;

        image.Resize((uint)(image.Width * scaleFactor), (uint)(image.Height * scaleFactor));
        await image.WriteAsync(outputPath);

        stream.Close();
        
        await loggingService.WriteGeneralLog($"from {initialResolution} to {PrettyResolution(image.Width, image.Height)} (x{scaleFactor})", texture);
        
        string PrettyResolution(uint width, uint height)
        {
            return $"{width}x{height}";
        }
    }
}