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

        try
        {
            using var image = new MagickImage(stream);
            image.FilterType = FilterType.Lanczos;

            var targetResolution = options.Targets.First(t => texture.TextureRelativePath.EndsWith(t.Key)).Value;

            float scaleFactor;
            if (image.Width < image.Height)
                scaleFactor = targetResolution / (float)image.Width;
            else
                scaleFactor = targetResolution / (float)image.Height;

            var initialResolution = (image.Width, image.Height);
            var resultResolution = (Width: (uint)(image.Width * scaleFactor), Height: (uint)(image.Height * scaleFactor));

            if (image.HasAlpha)
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

            await loggingService.WriteGeneralLog(
                $"From {PrettyResolution(initialResolution.Width, initialResolution.Height)} to {PrettyResolution(resultResolution.Width, resultResolution.Height)} (x{scaleFactor})",
                texture);
        }
        catch (Exception e)
        {
            await loggingService.WriteErrorLog($"Failed to resize {texture.TextureRelativePath}, reason : {e.Message}");
        }
        finally
        {
            await stream.DisposeAsync();
        }

        return;

        string PrettyResolution(uint width, uint height)
        {
            return $"{width}x{height}";
        }
    }
}