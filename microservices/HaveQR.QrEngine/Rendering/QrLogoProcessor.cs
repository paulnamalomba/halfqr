using System.Globalization;
using System.Text;
using System.Xml.Linq;
using HaveQR.Contracts.Enums;
using HaveQR.Contracts.Models;
using SkiaSharp;

namespace HaveQR.QrEngine.Rendering;

internal static class QrLogoProcessor
{
    private static readonly HashSet<string> AllowedSvgElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "svg",
        "g",
        "path",
        "rect",
        "circle",
        "ellipse",
        "polygon",
        "polyline",
        "line",
        "defs",
        "clipPath",
        "mask",
        "linearGradient",
        "radialGradient",
        "stop",
        "title",
        "desc",
        "use",
    };

    public static QrPreparedLogo? Prepare(QrLogoOptions? logoOptions)
    {
        if (logoOptions is null)
        {
            return null;
        }

        return logoOptions.SourceType switch
        {
            QrLogoSourceType.Svg => PrepareSvg(logoOptions),
            QrLogoSourceType.Png or QrLogoSourceType.Jpeg => PrepareRaster(logoOptions),
            _ => null,
        };
    }

    private static QrPreparedLogo PrepareSvg(QrLogoOptions logoOptions)
    {
        var document = XDocument.Parse(logoOptions.Svg!, LoadOptions.PreserveWhitespace);
        var root = document.Root ?? throw new InvalidOperationException("SVG logo payload must include a root svg element.");

        if (!string.Equals(root.Name.LocalName, "svg", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("SVG logo payload must start with an svg element.");
        }

        ValidateSvgTree(root);
        var (viewBox, width, height) = ResolveViewBox(root);
        root.SetAttributeValue("viewBox", viewBox);
        root.Attribute("x")?.Remove();
        root.Attribute("y")?.Remove();
        root.Attribute("width")?.Remove();
        root.Attribute("height")?.Remove();
        root.Attribute("preserveAspectRatio")?.Remove();

        var innerMarkup = string.Concat(root.Nodes().Select(static node => node.ToString(SaveOptions.DisableFormatting)));
        return new QrPreparedLogo(QrLogoSourceType.Svg, innerMarkup, width, height, null, viewBox);
    }

    private static QrPreparedLogo PrepareRaster(QrLogoOptions logoOptions)
    {
        var rawBytes = Convert.FromBase64String(logoOptions.ContentBase64!);
        using var bitmap = SKBitmap.Decode(rawBytes) ?? throw new InvalidOperationException("Unable to decode the uploaded raster logo.");

        if (bitmap.Width <= 0 || bitmap.Height <= 0 || bitmap.Width * bitmap.Height > 2_000_000)
        {
            throw new InvalidOperationException("Uploaded raster logos must stay below two million pixels.");
        }

        using var workingBitmap = new SKBitmap(bitmap.Width, bitmap.Height, true);
        using (var canvas = new SKCanvas(workingBitmap))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.DrawBitmap(bitmap, 0, 0);
            canvas.Flush();
        }

        if (logoOptions.RemoveBackground)
        {
            ApplyFlatBackgroundRemoval(workingBitmap);
        }

        using var image = SKImage.FromBitmap(workingBitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        var dataUri = $"data:image/png;base64,{Convert.ToBase64String(encoded.ToArray())}";
        return new QrPreparedLogo(logoOptions.SourceType, dataUri, workingBitmap.Width, workingBitmap.Height, "image/png", null);
    }

    private static void ValidateSvgTree(XElement root)
    {
        foreach (var element in root.DescendantsAndSelf())
        {
            if (!AllowedSvgElements.Contains(element.Name.LocalName))
            {
                throw new InvalidOperationException($"SVG element '{element.Name.LocalName}' is not allowed in uploaded logos.");
            }

            foreach (var attribute in element.Attributes().ToArray())
            {
                var attributeName = attribute.Name.LocalName;
                var value = attribute.Value;

                if (attributeName.StartsWith("on", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Event handler attributes are not allowed in uploaded SVG logos.");
                }

                if (value.Contains("javascript:", StringComparison.OrdinalIgnoreCase)
                    || value.Contains("expression(", StringComparison.OrdinalIgnoreCase)
                    || value.Contains("data:text/html", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Uploaded SVG logos cannot contain executable attribute values.");
                }

                if (attributeName is "href" or "xlink:href")
                {
                    if (!string.Equals(element.Name.LocalName, "use", StringComparison.OrdinalIgnoreCase) || !value.StartsWith('#'))
                    {
                        throw new InvalidOperationException("Uploaded SVG logos cannot reference external resources.");
                    }
                }

                if (string.Equals(attributeName, "style", StringComparison.OrdinalIgnoreCase)
                    && value.Contains("url(", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Uploaded SVG logos cannot use external url() style references.");
                }
            }
        }
    }

    private static (string ViewBox, double Width, double Height) ResolveViewBox(XElement root)
    {
        var viewBox = root.Attribute("viewBox")?.Value;

        if (!string.IsNullOrWhiteSpace(viewBox))
        {
            var parts = viewBox
                .Split([' ', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(static part => double.Parse(part, CultureInfo.InvariantCulture))
                .ToArray();

            if (parts.Length == 4 && parts[2] > 0 && parts[3] > 0)
            {
                return (viewBox, parts[2], parts[3]);
            }
        }

        var width = ParseSvgDimension(root.Attribute("width")?.Value) ?? 100d;
        var height = ParseSvgDimension(root.Attribute("height")?.Value) ?? width;
        return ($"0 0 {Format(width)} {Format(height)}", width, height);
    }

    private static double? ParseSvgDimension(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var numeric = new string(value.Trim().TakeWhile(static character => char.IsDigit(character) || character is '.' or '-').ToArray());

        return double.TryParse(numeric, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) && parsed > 0
            ? parsed
            : null;
    }

    private static void ApplyFlatBackgroundRemoval(SKBitmap bitmap)
    {
        var borderSamples = GetBorderSamples(bitmap);

        if (borderSamples.Count == 0)
        {
            return;
        }

        var reference = Average(borderSamples);
        var similarSamples = borderSamples.Count(sample => ColorDistance(sample, reference) <= 48d);

        if (similarSamples < borderSamples.Count * 0.75 || GetLuminance(reference) < 165d)
        {
            return;
        }

        var visited = new bool[bitmap.Width * bitmap.Height];
        var queue = new Queue<(int X, int Y)>();

        EnqueueBorderPixels(bitmap, queue, visited, reference);

        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            var pixel = bitmap.GetPixel(x, y);

            if (ColorDistance(pixel, reference) > 58d)
            {
                continue;
            }

            bitmap.SetPixel(x, y, pixel.WithAlpha(0));

            TryEnqueue(bitmap, x - 1, y, queue, visited, reference);
            TryEnqueue(bitmap, x + 1, y, queue, visited, reference);
            TryEnqueue(bitmap, x, y - 1, queue, visited, reference);
            TryEnqueue(bitmap, x, y + 1, queue, visited, reference);
        }
    }

    private static List<SKColor> GetBorderSamples(SKBitmap bitmap)
    {
        var step = Math.Max(1, Math.Min(bitmap.Width, bitmap.Height) / 24);
        var samples = new List<SKColor>();

        for (var x = 0; x < bitmap.Width; x += step)
        {
            samples.Add(bitmap.GetPixel(x, 0));
            samples.Add(bitmap.GetPixel(x, bitmap.Height - 1));
        }

        for (var y = 0; y < bitmap.Height; y += step)
        {
            samples.Add(bitmap.GetPixel(0, y));
            samples.Add(bitmap.GetPixel(bitmap.Width - 1, y));
        }

        return samples;
    }

    private static void EnqueueBorderPixels(SKBitmap bitmap, Queue<(int X, int Y)> queue, bool[] visited, SKColor reference)
    {
        for (var x = 0; x < bitmap.Width; x++)
        {
            TryEnqueue(bitmap, x, 0, queue, visited, reference);
            TryEnqueue(bitmap, x, bitmap.Height - 1, queue, visited, reference);
        }

        for (var y = 0; y < bitmap.Height; y++)
        {
            TryEnqueue(bitmap, 0, y, queue, visited, reference);
            TryEnqueue(bitmap, bitmap.Width - 1, y, queue, visited, reference);
        }
    }

    private static void TryEnqueue(SKBitmap bitmap, int x, int y, Queue<(int X, int Y)> queue, bool[] visited, SKColor reference)
    {
        if (x < 0 || y < 0 || x >= bitmap.Width || y >= bitmap.Height)
        {
            return;
        }

        var index = (y * bitmap.Width) + x;

        if (visited[index])
        {
            return;
        }

        visited[index] = true;

        if (ColorDistance(bitmap.GetPixel(x, y), reference) <= 58d)
        {
            queue.Enqueue((x, y));
        }
    }

    private static SKColor Average(IReadOnlyCollection<SKColor> samples)
    {
        var red = 0d;
        var green = 0d;
        var blue = 0d;

        foreach (var sample in samples)
        {
            red += sample.Red;
            green += sample.Green;
            blue += sample.Blue;
        }

        var divisor = samples.Count == 0 ? 1d : samples.Count;
        return new SKColor((byte)(red / divisor), (byte)(green / divisor), (byte)(blue / divisor));
    }

    private static double ColorDistance(SKColor left, SKColor right)
    {
        var deltaRed = left.Red - right.Red;
        var deltaGreen = left.Green - right.Green;
        var deltaBlue = left.Blue - right.Blue;
        return Math.Sqrt((deltaRed * deltaRed) + (deltaGreen * deltaGreen) + (deltaBlue * deltaBlue));
    }

    private static double GetLuminance(SKColor color)
        => (0.2126d * color.Red) + (0.7152d * color.Green) + (0.0722d * color.Blue);

    private static string Format(double value)
        => value.ToString("0.###", CultureInfo.InvariantCulture);
}

internal sealed record QrPreparedLogo(
    QrLogoSourceType SourceType,
    string Content,
    double Width,
    double Height,
    string? MimeType,
    string? ViewBox);