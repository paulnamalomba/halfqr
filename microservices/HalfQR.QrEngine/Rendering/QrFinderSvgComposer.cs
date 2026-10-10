using System.Text;
using HalfQR.Contracts.Enums;
using HalfQR.Contracts.Models;
using static HalfQR.QrEngine.Rendering.SvgNumberFormat;

namespace HalfQR.QrEngine.Rendering;

internal static class QrFinderSvgComposer
{
    internal const int QuietZoneModules = 4;
    internal const int FinderSizeModules = 7;
    private const int FinderInnerSizeModules = 5;
    private const int FinderCenterSizeModules = 3;

    internal static string BuildOverlay(int moduleCount, QrFinderOptions finderOptions, QrColorOptions colors)
    {
        var origins = new (int X, int Y)[]
        {
            (QuietZoneModules, QuietZoneModules),
            (moduleCount - QuietZoneModules - FinderSizeModules, QuietZoneModules),
            (QuietZoneModules, moduleCount - QuietZoneModules - FinderSizeModules),
        };

        var builder = new StringBuilder();
        builder.AppendLine();
        builder.AppendLine("<g id=\"halfqr-finder-compositor\" shape-rendering=\"geometricPrecision\">");

        foreach (var (x, y) in origins)
        {
            AppendFinder(builder, x, y, finderOptions, colors);
        }

        builder.Append("</g>");
        return builder.ToString();
    }

    private static void AppendFinder(StringBuilder builder, int originX, int originY, QrFinderOptions finderOptions, QrColorOptions colors)
    {
        AppendRect(builder, originX, originY, FinderSizeModules, FinderSizeModules, colors.Light, cornerRadius: 0);
        AppendShape(builder, finderOptions.BorderShape, originX, originY, FinderSizeModules, colors.Dark);
        AppendShape(builder, finderOptions.BorderShape, originX + 1, originY + 1, FinderInnerSizeModules, colors.Light);
        AppendShape(builder, finderOptions.CenterShape, originX + 2, originY + 2, FinderCenterSizeModules, colors.Dark);
    }

    private static void AppendShape(StringBuilder builder, QrFinderShape shape, double x, double y, double size, string fill)
    {
        switch (shape)
        {
            case QrFinderShape.Circle:
                AppendCircle(builder, x + (size / 2d), y + (size / 2d), size / 2d, fill);
                break;

            case QrFinderShape.Rounded:
                AppendRect(builder, x, y, size, size, fill, GetRoundedCornerRadius(size));
                break;

            default:
                AppendRect(builder, x, y, size, size, fill, cornerRadius: 0);
                break;
        }
    }

    private static void AppendRect(StringBuilder builder, double x, double y, double width, double height, string fill, double cornerRadius)
    {
        builder.Append("<rect x=\"")
            .Append(Format(x))
            .Append("\" y=\"")
            .Append(Format(y))
            .Append("\" width=\"")
            .Append(Format(width))
            .Append("\" height=\"")
            .Append(Format(height))
            .Append("\" fill=\"")
            .Append(fill)
            .Append('"');

        if (cornerRadius > 0)
        {
            builder.Append(" rx=\"")
                .Append(Format(cornerRadius))
                .Append("\" ry=\"")
                .Append(Format(cornerRadius))
                .Append('"');
        }

        builder.AppendLine("/>");
    }

    private static void AppendCircle(StringBuilder builder, double centerX, double centerY, double radius, string fill)
    {
        builder.Append("<circle cx=\"")
            .Append(Format(centerX))
            .Append("\" cy=\"")
            .Append(Format(centerY))
            .Append("\" r=\"")
            .Append(Format(radius))
            .Append("\" fill=\"")
            .Append(fill)
            .AppendLine("\"/>");
    }

    private static double GetRoundedCornerRadius(double size)
        => size switch
        {
            <= FinderCenterSizeModules => 0.85d,
            <= FinderInnerSizeModules => 1.25d,
            _ => 1.75d,
        };
}