using HaveQR.Contracts.Enums;

namespace HaveQR.Contracts.Responses;

public sealed record RenderJobAcceptedResponse(
    Guid JobId,
    QrJobStatus Status,
    string StatusUrl);