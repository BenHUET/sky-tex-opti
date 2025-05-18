using ImageMagick;
using SkyTexOpti.POCO;

namespace SkyTexOpti.Services;

public class ImageMagickResizerService(ILoggingService loggingService) : IResizerService
{
    public async Task Resize(Stream stream, Texture texture, string outputPath, uint targetResolution)
    {
        using var image = new MagickImage(stream);
        image.FilterType = FilterType.Lanczos;

        float scaleFactor;
        if (image.Width < image.Height)
            scaleFactor = targetResolution / (float)image.Width;
        else
            scaleFactor = targetResolution / (float)image.Height;

        var initialResolution = (image.Width, image.Height);
        var resultResolution = (Width: (uint)(image.Width * scaleFactor), Height: (uint)(image.Height * scaleFactor));

        if (!image.IsOpaque)
        {
            using var alphaChannel = image.Separate(Channels.Alpha)[0];
            
            image.Alpha(AlphaOption.Off);
            image.ColorSpace = ColorSpace.RGB;
            
            alphaChannel.Resize(resultResolution.Width, resultResolution.Height);
            image.Resize(resultResolution.Width, resultResolution.Height);

            image.ColorSpace = ColorSpace.sRGB;
            image.Alpha(AlphaOption.On);
            
            image.Composite(alphaChannel, CompositeOperator.CopyAlpha);
            
            await image.WriteAsync(outputPath);
        }
        else
        {
            image.Settings.Compression = CompressionMethod.DXT1;

            image.Resize(resultResolution.Width, resultResolution.Height);

            await image.WriteAsync(outputPath);
        }

        await loggingService.WriteGeneralLog($"From {initialResolution.Width}x{initialResolution.Height} to {resultResolution.Width}x{resultResolution.Height} (x{scaleFactor})", texture);
    }
}