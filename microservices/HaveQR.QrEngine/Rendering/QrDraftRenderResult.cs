namespace HaveQR.QrEngine.Rendering;

public sealed record QrDraftRenderResult(
    string ResolvedTargetUrl,
    string EncodedPayload,
    string ConfigurationHash,
    string PayloadHash,
    string SvgMarkup);