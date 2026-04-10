using System.Text;
using SkiaSharp;
using Svg.Skia;

namespace HaveQR.QrEngine.Rendering;

public static class QrArtifactRasterizer
{
    public static byte[] RasterizeSvgToPng(string svgMarkup, int sizePx)
    {
        using var svg = new SKSvg();
        using var svgStream = new MemoryStream(Encoding.UTF8.GetBytes(svgMarkup));
        var picture = svg.Load(svgStream) ?? throw new InvalidOperationException("Failed to load rendered SVG into the rasterizer.");
        var bounds = picture.CullRect;

        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            throw new InvalidOperationException("Rendered SVG did not expose valid bounds for PNG rasterization.");
        }

        var imageInfo = new SKImageInfo(sizePx, sizePx);
        using var surface = SKSurface.Create(imageInfo) ?? throw new InvalidOperationException("Failed to create a raster surface for PNG output.");
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        var scale = Math.Min(sizePx / bounds.Width, sizePx / bounds.Height);
        var translateX = (sizePx - (bounds.Width * scale)) / 2f;
        var translateY = (sizePx - (bounds.Height * scale)) / 2f;

        canvas.Translate(translateX, translateY);
        canvas.Scale(scale);
        canvas.DrawPicture(picture);
        canvas.Flush();

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}