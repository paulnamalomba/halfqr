using HalfQR.Contracts.Enums;

namespace HalfQR.Contracts.Responses;

public sealed record RenderJobAcceptedResponse(
    Guid JobId,
    QrJobStatus Status,
    string StatusUrl);