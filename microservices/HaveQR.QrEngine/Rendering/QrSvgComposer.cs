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

    public static string Compose(QRCodeData qrCodeData, SubmitRenderJobRequest request, QrPreparedLogo? logo)
    {
        var moduleCount = qrCodeData.ModuleMatrix.Count;
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

                AppendModule(builder, request.Data.Pattern, column, row);
            }
        }

        builder.AppendLine("</g>");
        builder.AppendLine(QrFinderSvgComposer.BuildOverlay(moduleCount, request.Finder, request.Colors));

        if (logo is not null && request.Logo is not null)
        {
            AppendLogo(builder, logo, request.Logo, request.Colors.Light, moduleCount);
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

        var angle = request.Data.GradientRotation * (Math.PI / 180d);
        var x1 = 0.5d - (0.5d * Math.Cos(angle));
        var y1 = 0.5d - (0.5d * Math.Sin(angle));
        var x2 = 0.5d + (0.5d * Math.Cos(angle));
        var y2 = 0.5d + (0.5d * Math.Sin(angle));

        builder.AppendLine("<defs>");
        builder.Append("<linearGradient id=\"haveqr-data-gradient\" gradientUnits=\"objectBoundingBox\" x1=\"")
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

    private static void AppendLogo(StringBuilder builder, QrPreparedLogo logo, QrLogoOptions logoOptions, string backdropFill, int moduleCount)
    {
        var activeCodeSize = moduleCount - (QuietZoneModules * 2d);
        var logoBoxSize = activeCodeSize * (logoOptions.SizePercent / 100d);
        var aspectRatio = logo.Width <= 0 || logo.Height <= 0 ? 1d : logo.Width / logo.Height;
        var logoWidth = aspectRatio >= 1d ? logoBoxSize : logoBoxSize * aspectRatio;
        var logoHeight = aspectRatio >= 1d ? logoBoxSize / aspectRatio : logoBoxSize;
        var x = (moduleCount - logoWidth) / 2d;
        var y = (moduleCount - logoHeight) / 2d;
        var backdropWidth = logoWidth * (1d + (logoOptions.BackdropPaddingPercent / 100d));
        var backdropHeight = logoHeight * (1d + (logoOptions.BackdropPaddingPercent / 100d));
        var backdropX = (moduleCount - backdropWidth) / 2d;
        var backdropY = (moduleCount - backdropHeight) / 2d;
        var cornerRadius = Math.Min(backdropWidth, backdropHeight) * 0.22d;

        builder.Append("<g id=\"haveqr-logo\">\n<rect x=\"")
            .Append(Format(backdropX))
            .Append("\" y=\"")
            .Append(Format(backdropY))
            .Append("\" width=\"")
            .Append(Format(backdropWidth))
            .Append("\" height=\"")
            .Append(Format(backdropHeight))
            .Append("\" rx=\"")
            .Append(Format(cornerRadius))
            .Append("\" ry=\"")
            .Append(Format(cornerRadius))
            .Append("\" fill=\"")
            .Append(backdropFill)
            .AppendLine("\"/>");

        if (logo.SourceType == QrLogoSourceType.Svg)
        {
            builder.Append("<svg x=\"")
                .Append(Format(x))
                .Append("\" y=\"")
                .Append(Format(y))
                .Append("\" width=\"")
                .Append(Format(logoWidth))
                .Append("\" height=\"")
                .Append(Format(logoHeight))
                .Append("\" viewBox=\"")
                .Append(logo.ViewBox)
                .AppendLine("\" preserveAspectRatio=\"xMidYMid meet\">");
            builder.Append(logo.Content);
            builder.AppendLine("</svg>");
            builder.AppendLine("</g>");
            return;
        }

        builder.Append("<image x=\"")
            .Append(Format(x))
            .Append("\" y=\"")
            .Append(Format(y))
            .Append("\" width=\"")
            .Append(Format(logoWidth))
            .Append("\" height=\"")
            .Append(Format(logoHeight))
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

    private static string Format(double value)
        => value.ToString("0.###", CultureInfo.InvariantCulture);
}