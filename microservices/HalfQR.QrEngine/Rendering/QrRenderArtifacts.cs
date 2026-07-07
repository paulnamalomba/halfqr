namespace HalfQR.QrEngine.Rendering;

public sealed record QrRenderArtifacts(
    string ResolvedTargetUrl,
    string EncodedPayload,
    string ConfigurationHash,
    string PayloadHash,
    string SvgMarkup,
    byte[] PngBytes);