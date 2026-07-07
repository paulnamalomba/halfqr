using HalfQR.Contracts.Enums;

namespace HalfQR.Contracts.Responses;

public sealed record RenderJobStatusResponse
{
    public Guid JobId { get; init; }

    public QrJobStatus Status { get; init; }

    public QrContentType ContentType { get; init; }

    public string? EncodedPayload { get; init; }

    public string? ResolvedTargetUrl { get; init; }

    public string? ConfigurationHash { get; init; }

    public string? PayloadHash { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? StartedAt { get; init; }

    public DateTimeOffset? CompletedAt { get; init; }

    public string? FailureReason { get; init; }

    public IReadOnlyList<RenderArtifactDescriptor> Artifacts { get; init; } = [];
}