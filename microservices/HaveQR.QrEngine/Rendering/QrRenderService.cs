using System.Text.Json;
using HaveQR.Contracts.Enums;
using HaveQR.Contracts.Requests;
using HaveQR.QrEngine.Hashing;
using HaveQR.QrEngine.PayloadEncoding;
using QRCoder;

namespace HaveQR.QrEngine.Rendering;

public sealed class QrRenderService(
    IQrPayloadEncoder payloadEncoder,
    IHashService hashService) : IQrRenderService
{
    public Task<QrDraftRenderResult> RenderDraftAsync(SubmitRenderJobRequest request, CancellationToken cancellationToken)
    {
        var draft = BuildDraftRenderResult(request, cancellationToken);
        return Task.FromResult(draft);
    }

    public Task<QrRenderArtifacts> RenderAsync(SubmitRenderJobRequest request, CancellationToken cancellationToken)
    {
        var draft = BuildDraftRenderResult(request, cancellationToken);
        var pngBytes = QrArtifactRasterizer.RasterizeSvgToPng(draft.SvgMarkup, request.Output.SizePx);

        return Task.FromResult(new QrRenderArtifacts(
            ResolvedTargetUrl: draft.ResolvedTargetUrl,
            EncodedPayload: draft.EncodedPayload,
            ConfigurationHash: draft.ConfigurationHash,
            PayloadHash: draft.PayloadHash,
            SvgMarkup: draft.SvgMarkup,
            PngBytes: pngBytes));
    }

    private QrDraftRenderResult BuildDraftRenderResult(SubmitRenderJobRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        QrRenderRequestValidator.EnsureValid(request);

        var encodedPayload = payloadEncoder.Encode(request.ContentType, request.TargetUrl, request.Payload);
        var configurationHash = hashService.Compute(SerializeCanonicalRequest(request));
        var payloadHash = hashService.Compute(encodedPayload);
        var svgMarkup = GenerateSvg(encodedPayload, request);

        return new QrDraftRenderResult(
            ResolvedTargetUrl: encodedPayload,
            EncodedPayload: encodedPayload,
            ConfigurationHash: configurationHash,
            PayloadHash: payloadHash,
            SvgMarkup: svgMarkup);
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
            request.Data,
            Logo = request.Logo is null
                ? null
                : new
                {
                    request.Logo.SourceType,
                    HasSvg = !string.IsNullOrWhiteSpace(request.Logo.Svg),
                    HasRaster = !string.IsNullOrWhiteSpace(request.Logo.ContentBase64),
                    request.Logo.ContentType,
                    request.Logo.SizePercent,
                    request.Logo.RemoveBackground,
                    request.Logo.BackdropPaddingPercent,
                },
        };

        return JsonSerializer.Serialize(canonical);
    }

    private static string GenerateSvg(string encodedPayload, SubmitRenderJobRequest request)
    {
        using var generator = new QRCodeGenerator();
        QRCodeData qrCodeData;

        try
        {
            qrCodeData = generator.CreateQrCode(encodedPayload, ToEccLevel(request.ErrorCorrectionLevel));
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                "Unable to encode the QR payload for rendering. Try a shorter URL or reduce the ECC level.",
                exception);
        }

        using (qrCodeData)
        {
            var preparedLogo = QrLogoProcessor.Prepare(request.Logo);
            return QrSvgComposer.Compose(qrCodeData, request, preparedLogo);
        }
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