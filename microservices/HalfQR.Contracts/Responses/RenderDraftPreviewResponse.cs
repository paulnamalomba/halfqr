namespace HalfQR.Contracts.Responses;

public sealed record RenderDraftPreviewResponse(
    string ResolvedTargetUrl,
    string EncodedPayload,
    string ConfigurationHash,
    string PayloadHash,
    string SvgMarkup);