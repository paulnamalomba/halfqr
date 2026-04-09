using HaveQR.Contracts.Enums;
using HaveQR.Contracts.Requests;

namespace HaveQR.Contracts.Models;

public sealed record RenderJobState
{
    public Guid JobId { get; init; }

    public SubmitRenderJobRequest Request { get; init; } = new();

    public QrJobStatus Status { get; init; } = QrJobStatus.Queued;

    public string? EncodedPayload { get; init; }

    public string? ResolvedTargetUrl { get; init; }

    public string? ConfigurationHash { get; init; }

    public string? PayloadHash { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? StartedAt { get; init; }

    public DateTimeOffset? CompletedAt { get; init; }

    public string? FailureReason { get; init; }

    public IReadOnlyList<RenderArtifactState> Artifacts { get; init; } = [];
}