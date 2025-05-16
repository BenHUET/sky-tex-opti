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

            var targetResolution = options.Targets.First(t => texture.TextureRelativePath.EndsWith(t.Key)).Value;
            var initialResolution = PrettyResolution(image.Width, image.Height);

            float scaleFactor;
            if (image.Width < image.Height)
                scaleFactor = targetResolution / (float)image.Width;
            else
                scaleFactor = targetResolution / (float)image.Height;

            if (image.HasAlpha)
            {
                using var combinedImage = new MagickImage(MagickColors.Black, (uint)(image.Width * scaleFactor), (uint)(image.Height * scaleFactor));

                var channels = image.Separate();
                for (var i = 0; i < channels.Count; i++)
                {
                    var channel = channels[i];

                    channel.FilterType = FilterType.Lanczos;
                    channel.Resize((uint)(image.Width * scaleFactor), (uint)(image.Height * scaleFactor));

                    combinedImage.Composite(channel, i switch
                    {
                        0 => CompositeOperator.CopyRed,
                        1 => CompositeOperator.CopyGreen,
                        2 => CompositeOperator.CopyBlue,
                        3 => CompositeOperator.CopyAlpha,
                        _ => throw new ArgumentOutOfRangeException(null, "Something went wrong processing image's channels.")
                    });

                    channel.Dispose();
                }

                await combinedImage.WriteAsync(outputPath);
            }
            else
            {
                image.FilterType = FilterType.Lanczos;
                image.Resize((uint)(image.Width * scaleFactor), (uint)(image.Height * scaleFactor));
                image.Settings.SetDefine(MagickFormat.Dds, "compression", "dxt1");
                await image.WriteAsync(outputPath);
            }
            
            await loggingService.WriteGeneralLog($"From {initialResolution} to {PrettyResolution(image.Width, image.Height)} (x{scaleFactor})", texture);
        }
        catch (Exception e)
        {
            await loggingService.WriteErrorLog($"Failed to resize {texture.TextureRelativePath}, reason : {e.Message}");
            throw;
        }

        return;

        string PrettyResolution(uint width, uint height)
        {
            return $"{width}x{height}";
        }
    }
}