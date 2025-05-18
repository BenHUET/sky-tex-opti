using ImageMagick;
using SkyTexOpti.POCO;

namespace SkyTexOpti.Services;

public class ImageMagickResizerService : IResizerService
{
    public async Task Resize(Stream stream, Texture texture, string outputPath, float scaleFactor)
    {
        using var image = new MagickImage(stream);
        image.FilterType = FilterType.Lanczos;
        
        var targetWidth = (uint)(texture.Width! * scaleFactor);
        var targetHeight = (uint)(texture.Height! * scaleFactor);

        if (!image.IsOpaque)
        {
            using var alphaChannel = image.Separate(Channels.Alpha)[0];
            
            image.Alpha(AlphaOption.Off);
            image.ColorSpace = ColorSpace.RGB;
            
            alphaChannel.Resize(targetWidth, targetHeight);
            image.Resize(targetWidth, targetHeight);

            image.ColorSpace = ColorSpace.sRGB;
            image.Alpha(AlphaOption.On);
            
            image.Composite(alphaChannel, CompositeOperator.CopyAlpha);
            
            await image.WriteAsync(outputPath);
        }
        else
        {
            image.Settings.Compression = CompressionMethod.DXT1;

            image.Resize(targetWidth, targetHeight);

            await image.WriteAsync(outputPath);
        }
    }
}