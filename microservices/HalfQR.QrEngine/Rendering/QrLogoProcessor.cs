using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using HalfQR.Contracts.Enums;
using HalfQR.Contracts.Models;
using SkiaSharp;
using static HalfQR.QrEngine.Rendering.SvgNumberFormat;

namespace HalfQR.QrEngine.Rendering;

internal static class QrLogoProcessor
{
    private const int MaxRasterPixels = 2_000_000;
    private const long MaxDecodedRasterPixels = 4096L * 4096L;
    private const int MaxSvgElements = 4_000;
    private const int MaxSvgUseElements = 64;

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
        "metadata",
        "style",
        "symbol",
        "text",
        "tspan",
        "use",
        "image",
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
        var document = ParseSvgDocument(logoOptions.Svg!);
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
        EnsureDecodableRasterSize(rawBytes);
        using var bitmap = SKBitmap.Decode(rawBytes) ?? throw new InvalidOperationException("Unable to decode the uploaded raster logo.");

        if (bitmap.Width <= 0 || bitmap.Height <= 0)
        {
            throw new InvalidOperationException("Uploaded raster logos must have valid dimensions.");
        }

        var (targetWidth, targetHeight) = ResolveRasterDimensions(bitmap.Width, bitmap.Height);

        using var workingBitmap = new SKBitmap(targetWidth, targetHeight, true);
        using (var canvas = new SKCanvas(workingBitmap))
        using (var paint = new SKPaint { FilterQuality = SKFilterQuality.High, IsAntialias = true })
        {
            canvas.Clear(SKColors.Transparent);
            canvas.DrawBitmap(bitmap, SKRect.Create(targetWidth, targetHeight), paint);
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

    // Read the header dimensions first so a small compressed file cannot force a multi-gigabyte decode.
    private static void EnsureDecodableRasterSize(byte[] rawBytes)
    {
        using var stream = new MemoryStream(rawBytes, writable: false);
        using var codec = SKCodec.Create(stream) ?? throw new InvalidOperationException("Unable to decode the uploaded raster logo.");

        if (codec.Info.Width <= 0 || codec.Info.Height <= 0)
        {
            throw new InvalidOperationException("Uploaded raster logos must have valid dimensions.");
        }

        if ((long)codec.Info.Width * codec.Info.Height > MaxDecodedRasterPixels)
        {
            throw new InvalidOperationException("Uploaded raster logos must be 4096 x 4096 pixels or smaller.");
        }
    }

    private static XDocument ParseSvgDocument(string svg)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersFromEntities = 0,
        };

        try
        {
            using var stringReader = new StringReader(svg);
            using var xmlReader = XmlReader.Create(stringReader, settings);
            return XDocument.Load(xmlReader, LoadOptions.PreserveWhitespace);
        }
        catch (XmlException)
        {
            throw new InvalidOperationException("SVG logo payload must be well-formed XML without a DTD.");
        }
    }

    private static (int Width, int Height) ResolveRasterDimensions(int width, int height)
    {
        var pixelCount = (long)width * height;

        if (pixelCount <= MaxRasterPixels)
        {
            return (width, height);
        }

        var scale = Math.Sqrt(MaxRasterPixels / (double)pixelCount);
        var scaledWidth = Math.Max(1, (int)Math.Floor(width * scale));
        var scaledHeight = Math.Max(1, (int)Math.Floor(height * scale));

        while ((long)scaledWidth * scaledHeight > MaxRasterPixels)
        {
            if (scaledWidth >= scaledHeight && scaledWidth > 1)
            {
                scaledWidth -= 1;
                continue;
            }

            if (scaledHeight > 1)
            {
                scaledHeight -= 1;
                continue;
            }

            break;
        }

        return (scaledWidth, scaledHeight);
    }

    private static void ValidateSvgTree(XElement root)
    {
        var elements = root.DescendantsAndSelf().ToArray();

        if (elements.Length > MaxSvgElements)
        {
            throw new InvalidOperationException($"Uploaded SVG logos must contain {MaxSvgElements} elements or fewer.");
        }

        ValidateUseReferences(elements);

        foreach (var element in elements)
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
                    var isLocalReference = value.StartsWith('#');
                    var isEmbeddedRaster = string.Equals(element.Name.LocalName, "image", StringComparison.OrdinalIgnoreCase)
                        && IsSafeEmbeddedRaster(value);

                    if (!isLocalReference && !isEmbeddedRaster)
                    {
                        throw new InvalidOperationException("Uploaded SVG logos cannot reference external resources.");
                    }
                }

                // Presentation attributes such as fill, stroke, mask and filter accept url() too, so every value is checked.
                if (ContainsUnsafeCssUrl(value))
                {
                    throw new InvalidOperationException("Uploaded SVG logos cannot use external url() style references.");
                }
            }

            if (string.Equals(element.Name.LocalName, "style", StringComparison.OrdinalIgnoreCase))
            {
                ValidateSvgStylesheet(element.Value);
            }
        }
    }

    // A <use> may only point at plain shapes. Blocking references to other <use> elements stops nested amplification in the rasterizer.
    private static void ValidateUseReferences(XElement[] elements)
    {
        var useElements = elements
            .Where(static element => string.Equals(element.Name.LocalName, "use", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (useElements.Length == 0)
        {
            return;
        }

        if (useElements.Length > MaxSvgUseElements)
        {
            throw new InvalidOperationException($"Uploaded SVG logos must contain {MaxSvgUseElements} <use> elements or fewer.");
        }

        var elementsById = new Dictionary<string, XElement>(StringComparer.Ordinal);

        foreach (var element in elements)
        {
            if (element.Attribute("id")?.Value is { Length: > 0 } id)
            {
                elementsById.TryAdd(id, element);
            }
        }

        foreach (var useElement in useElements)
        {
            var reference = useElement.Attributes()
                .FirstOrDefault(static attribute => attribute.Name.LocalName == "href")?.Value;

            if (reference is null || !reference.StartsWith('#') || !elementsById.TryGetValue(reference[1..], out var target))
            {
                continue;
            }

            if (target.DescendantsAndSelf().Any(static element => string.Equals(element.Name.LocalName, "use", StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("Uploaded SVG logos cannot reference elements that contain other <use> elements.");
            }
        }
    }

    private static bool IsSafeEmbeddedRaster(string value)
        => value.StartsWith("data:image/png;base64,", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("data:image/jpeg;base64,", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("data:image/webp;base64,", StringComparison.OrdinalIgnoreCase);

    private static void ValidateSvgStylesheet(string stylesheet)
    {
        if (stylesheet.Contains("@import", StringComparison.OrdinalIgnoreCase)
            || stylesheet.Contains("expression(", StringComparison.OrdinalIgnoreCase)
            || stylesheet.Contains("javascript:", StringComparison.OrdinalIgnoreCase)
            || ContainsUnsafeCssUrl(stylesheet))
        {
            throw new InvalidOperationException("Uploaded SVG logo styles cannot reference external or executable resources.");
        }
    }

    private static bool ContainsUnsafeCssUrl(string css)
    {
        foreach (Match match in Regex.Matches(css, @"url\((?<value>[^)]*)\)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            var reference = match.Groups["value"].Value.Trim().Trim('\'', '"');

            if (!reference.StartsWith('#'))
            {
                return true;
            }
        }

        return false;
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
        var borderSamples = GetBorderSamples(bitmap)
            .Where(static sample => sample.Alpha >= 235)
            .ToArray();

        if (borderSamples.Length == 0)
        {
            return;
        }

        var reference = Average(borderSamples);
        var similarSamples = borderSamples.Count(sample => IsRemovableBackgroundSample(sample, reference));

        if (similarSamples < borderSamples.Length * 0.75 || !IsNearWhite(reference))
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

            if (!IsRemovableBackgroundSample(pixel, reference))
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

        if (IsRemovableBackgroundSample(bitmap.GetPixel(x, y), reference))
        {
            queue.Enqueue((x, y));
        }
    }

    private static bool IsNearWhite(SKColor color)
        => color.Alpha >= 235
            && color.Red >= 215
            && color.Green >= 215
            && color.Blue >= 215
            && GetChannelSpread(color) <= 24d;

    private static bool IsRemovableBackgroundSample(SKColor sample, SKColor reference)
    {
        if (sample.Alpha < 235)
        {
            return false;
        }

        if (sample.Red < 185 || sample.Green < 185 || sample.Blue < 185)
        {
            return false;
        }

        if (GetChannelSpread(sample) > 28d)
        {
            return false;
        }

        return ColorDistance(sample, reference) <= 46d;
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

    private static double GetChannelSpread(SKColor color)
    {
        var max = Math.Max(color.Red, Math.Max(color.Green, color.Blue));
        var min = Math.Min(color.Red, Math.Min(color.Green, color.Blue));
        return max - min;
    }
}

internal sealed record QrPreparedLogo(
    QrLogoSourceType SourceType,
    string Content,
    double Width,
    double Height,
    string? MimeType,
    string? ViewBox);
