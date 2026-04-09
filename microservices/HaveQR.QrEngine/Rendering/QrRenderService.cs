using System.Text;
using System.Text.Json;
using HaveQR.Contracts.Enums;
using HaveQR.Contracts.Requests;
using HaveQR.QrEngine.Hashing;
using HaveQR.QrEngine.PayloadEncoding;
using QRCoder;
using SkiaSharp;
using Svg.Skia;

namespace HaveQR.QrEngine.Rendering;

public sealed class QrRenderService(
    IQrPayloadEncoder payloadEncoder,
    IHashService hashService) : IQrRenderService
{
    public Task<QrRenderArtifacts> RenderAsync(SubmitRenderJobRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var encodedPayload = payloadEncoder.Encode(request.ContentType, request.TargetUrl, request.Payload);
        var configurationHash = hashService.Compute(SerializeCanonicalRequest(request));
        var payloadHash = hashService.Compute(encodedPayload);
        var svgMarkup = GenerateSvg(encodedPayload, request);
        var pngBytes = RasterizePng(svgMarkup, request.Output.SizePx);

        return Task.FromResult(new QrRenderArtifacts(
            ResolvedTargetUrl: encodedPayload,
            EncodedPayload: encodedPayload,
            ConfigurationHash: configurationHash,
            PayloadHash: payloadHash,
            SvgMarkup: svgMarkup,
            PngBytes: pngBytes));
    }

    private static string SerializeCanonicalRequest(SubmitRenderJobRequest request)
    {
        var canonical = new
        {
            ContentType = request.ContentType.ToString(),
            request.TargetUrl,
            request.Mode,
            request.ErrorCorrectionLevel,
            Payload = request.Payload
                .OrderBy(static entry => entry.Key, StringComparer.Ordinal)
                .ToDictionary(static entry => entry.Key, static entry => entry.Value, StringComparer.Ordinal),
            request.Output,
            request.Finder,
            request.Colors,
            Logo = request.Logo is null
                ? null
                : new
                {
                    HasSvg = !string.IsNullOrWhiteSpace(request.Logo.Svg),
                    request.Logo.SizePercent,
                },
        };

        return JsonSerializer.Serialize(canonical);
    }

    private static string GenerateSvg(string encodedPayload, SubmitRenderJobRequest request)
    {
        using var generator = new QRCodeGenerator();
        using var qrCodeData = generator.CreateQrCode(encodedPayload, ToEccLevel(request.ErrorCorrectionLevel));
        using var qrCode = new SvgQRCode(qrCodeData);

        SvgQRCode.SvgLogo? logo = null;

        if (!string.IsNullOrWhiteSpace(request.Logo?.Svg))
        {
            logo = new SvgQRCode.SvgLogo(request.Logo.Svg!, request.Logo.SizePercent, fillLogoBackground: true, iconEmbedded: true);
        }

        return qrCode.GetGraphic(
            pixelsPerModule: 20,
            darkColorHex: request.Colors.Dark,
            lightColorHex: request.Colors.Light,
            drawQuietZones: true,
            sizingMode: SvgQRCode.SizingMode.ViewBoxAttribute,
            logo: logo);
    }

    private static byte[] RasterizePng(string svgMarkup, int sizePx)
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
        using var surface = SKSurface.Create(imageInfo);
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

    private static QRCodeGenerator.ECCLevel ToEccLevel(QrErrorCorrectionLevel level)
        => level switch
        {
            QrErrorCorrectionLevel.L => QRCodeGenerator.ECCLevel.L,
            QrErrorCorrectionLevel.M => QRCodeGenerator.ECCLevel.M,
            QrErrorCorrectionLevel.Q => QRCodeGenerator.ECCLevel.Q,
            _ => QRCodeGenerator.ECCLevel.H,
        };
}