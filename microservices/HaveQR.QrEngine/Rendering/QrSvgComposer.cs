using System.Globalization;
using System.Text;
using HaveQR.Contracts.Enums;
using HaveQR.Contracts.Models;
using HaveQR.Contracts.Requests;
using QRCoder;

namespace HaveQR.QrEngine.Rendering;

internal static class QrSvgComposer
{
    private const int QuietZoneModules = 4;
    private const int FinderSizeModules = 7;
    private const double DottedRadius = 0.38d;

    private sealed record LogoLayout(
        double X,
        double Y,
        double Width,
        double Height,
        double BackdropX,
        double BackdropY,
        double BackdropWidth,
        double BackdropHeight,
        double CornerRadius);

    public static string Compose(QRCodeData qrCodeData, SubmitRenderJobRequest request, QrPreparedLogo? logo)
    {
        var moduleCount = qrCodeData.ModuleMatrix.Count;
        var logoLayout = logo is not null && request.Logo is not null
            ? ResolveLogoLayout(logo, request.Logo, moduleCount)
            : null;
        var builder = new StringBuilder();

        builder.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 ")
            .Append(moduleCount)
            .Append(' ')
            .Append(moduleCount)
            .AppendLine("\" role=\"img\" aria-label=\"HaveQR code\">");

        AppendDefs(builder, request, moduleCount);
        builder.Append("<rect x=\"0\" y=\"0\" width=\"")
            .Append(moduleCount)
            .Append("\" height=\"")
            .Append(moduleCount)
            .Append("\" fill=\"")
            .Append(request.Colors.Light)
            .AppendLine("\"/>");

        builder.Append("<g id=\"haveqr-data-modules\" fill=\"")
            .Append(ResolveDataFill(request))
            .AppendLine("\" shape-rendering=\"geometricPrecision\">");

        for (var row = 0; row < moduleCount; row++)
        {
            var moduleRow = qrCodeData.ModuleMatrix[row];

            for (var column = 0; column < moduleCount; column++)
            {
                if (!moduleRow[column] || IsInFinderWindow(row, column, moduleCount))
                {
                    continue;
                }

                if (logoLayout is not null && IsInLogoSafeRegion(row, column, logoLayout))
                {
                    continue;
                }

                AppendModule(builder, request.Data.Pattern, column, row);
            }
        }

        builder.AppendLine("</g>");
        builder.AppendLine(QrFinderSvgComposer.BuildOverlay(moduleCount, request.Finder, request.Colors));

        if (logo is not null && logoLayout is not null)
        {
            AppendLogo(builder, logo, request.Colors.Light, logoLayout);
        }

        builder.Append("</svg>");
        return builder.ToString();
    }

    private static void AppendDefs(StringBuilder builder, SubmitRenderJobRequest request, int moduleCount)
    {
        if (request.Data.GradientMode != QrGradientMode.Linear)
        {
            return;
        }

        var (x1, y1, x2, y2) = ResolveGradientVector(moduleCount, request.Data.GradientRotation);

        builder.AppendLine("<defs>");
        builder.Append("<linearGradient id=\"haveqr-data-gradient\" gradientUnits=\"userSpaceOnUse\" x1=\"")
            .Append(Format(x1))
            .Append("\" y1=\"")
            .Append(Format(y1))
            .Append("\" x2=\"")
            .Append(Format(x2))
            .Append("\" y2=\"")
            .Append(Format(y2))
            .AppendLine("\">");
        builder.Append("<stop offset=\"0%\" stop-color=\"")
            .Append(request.Data.GradientStart)
            .AppendLine("\"/>");
        builder.Append("<stop offset=\"100%\" stop-color=\"")
            .Append(request.Data.GradientEnd)
            .AppendLine("\"/>");
        builder.AppendLine("</linearGradient>");
        builder.AppendLine("</defs>");
    }

    private static void AppendModule(StringBuilder builder, QrDataPattern pattern, int x, int y)
    {
        if (pattern == QrDataPattern.Dotted)
        {
            builder.Append("<circle cx=\"")
                .Append(Format(x + 0.5d))
                .Append("\" cy=\"")
                .Append(Format(y + 0.5d))
                .Append("\" r=\"")
                .Append(Format(DottedRadius))
                .AppendLine("\"/>");
            return;
        }

        builder.Append("<rect x=\"")
            .Append(x)
            .Append("\" y=\"")
            .Append(y)
            .AppendLine("\" width=\"1\" height=\"1\"/>");
    }

    private static void AppendLogo(StringBuilder builder, QrPreparedLogo logo, string backdropFill, LogoLayout layout)
    {
        builder.Append("<g id=\"haveqr-logo\">\n<rect x=\"")
            .Append(Format(layout.BackdropX))
            .Append("\" y=\"")
            .Append(Format(layout.BackdropY))
            .Append("\" width=\"")
            .Append(Format(layout.BackdropWidth))
            .Append("\" height=\"")
            .Append(Format(layout.BackdropHeight))
            .Append("\" rx=\"")
            .Append(Format(layout.CornerRadius))
            .Append("\" ry=\"")
            .Append(Format(layout.CornerRadius))
            .Append("\" fill=\"")
            .Append(backdropFill)
            .AppendLine("\"/>");

        if (logo.SourceType == QrLogoSourceType.Svg)
        {
            builder.Append("<svg x=\"")
                .Append(Format(layout.X))
                .Append("\" y=\"")
                .Append(Format(layout.Y))
                .Append("\" width=\"")
                .Append(Format(layout.Width))
                .Append("\" height=\"")
                .Append(Format(layout.Height))
                .Append("\" viewBox=\"")
                .Append(logo.ViewBox)
                .AppendLine("\" preserveAspectRatio=\"xMidYMid meet\">");
            builder.Append(logo.Content);
            builder.AppendLine("</svg>");
            builder.AppendLine("</g>");
            return;
        }

        builder.Append("<image x=\"")
            .Append(Format(layout.X))
            .Append("\" y=\"")
            .Append(Format(layout.Y))
            .Append("\" width=\"")
            .Append(Format(layout.Width))
            .Append("\" height=\"")
            .Append(Format(layout.Height))
            .Append("\" href=\"")
            .Append(logo.Content)
            .AppendLine("\" preserveAspectRatio=\"xMidYMid meet\"/>");
        builder.AppendLine("</g>");
    }

    private static string ResolveDataFill(SubmitRenderJobRequest request)
        => request.Data.GradientMode == QrGradientMode.Linear
            ? "url(#haveqr-data-gradient)"
            : request.Colors.Dark;

    private static bool IsInFinderWindow(int row, int column, int moduleCount)
    {
        var maxOrigin = moduleCount - QuietZoneModules - FinderSizeModules;
        return (row >= QuietZoneModules && row < QuietZoneModules + FinderSizeModules && column >= QuietZoneModules && column < QuietZoneModules + FinderSizeModules)
            || (row >= QuietZoneModules && row < QuietZoneModules + FinderSizeModules && column >= maxOrigin && column < maxOrigin + FinderSizeModules)
            || (row >= maxOrigin && row < maxOrigin + FinderSizeModules && column >= QuietZoneModules && column < QuietZoneModules + FinderSizeModules);
    }

    private static (double X1, double Y1, double X2, double Y2) ResolveGradientVector(int moduleCount, int rotation)
    {
        var angle = rotation * (Math.PI / 180d);
        var directionX = Math.Cos(angle);
        var directionY = Math.Sin(angle);
        var activeCodeSize = moduleCount - (QuietZoneModules * 2d);
        var center = moduleCount / 2d;
        var maxDirection = Math.Max(Math.Max(Math.Abs(directionX), Math.Abs(directionY)), double.Epsilon);
        var halfSpan = (activeCodeSize / 2d) / maxDirection;

        return (
            center - (directionX * halfSpan),
            center - (directionY * halfSpan),
            center + (directionX * halfSpan),
            center + (directionY * halfSpan));
    }

    private static LogoLayout ResolveLogoLayout(QrPreparedLogo logo, QrLogoOptions logoOptions, int moduleCount)
    {
        var activeCodeSize = moduleCount - (QuietZoneModules * 2d);
        var logoBoxSize = activeCodeSize * (logoOptions.SizePercent / 100d);
        var aspectRatio = logo.Width <= 0 || logo.Height <= 0 ? 1d : logo.Width / logo.Height;
        var width = aspectRatio >= 1d ? logoBoxSize : logoBoxSize * aspectRatio;
        var height = aspectRatio >= 1d ? logoBoxSize / aspectRatio : logoBoxSize;
        var x = (moduleCount - width) / 2d;
        var y = (moduleCount - height) / 2d;
        var backdropWidth = width * (1d + (logoOptions.BackdropPaddingPercent / 100d));
        var backdropHeight = height * (1d + (logoOptions.BackdropPaddingPercent / 100d));
        var backdropX = (moduleCount - backdropWidth) / 2d;
        var backdropY = (moduleCount - backdropHeight) / 2d;
        var cornerRadius = Math.Min(backdropWidth, backdropHeight) * 0.22d;

        return new LogoLayout(
            x,
            y,
            width,
            height,
            backdropX,
            backdropY,
            backdropWidth,
            backdropHeight,
            cornerRadius);
    }

    private static bool IsInLogoSafeRegion(int row, int column, LogoLayout layout)
    {
        var moduleCenterX = column + 0.5d;
        var moduleCenterY = row + 0.5d;

        return moduleCenterX >= layout.BackdropX
            && moduleCenterX <= layout.BackdropX + layout.BackdropWidth
            && moduleCenterY >= layout.BackdropY
            && moduleCenterY <= layout.BackdropY + layout.BackdropHeight;
    }

    private static string Format(double value)
        => value.ToString("0.###", CultureInfo.InvariantCulture);
}