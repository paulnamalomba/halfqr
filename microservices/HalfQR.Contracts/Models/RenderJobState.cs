using HalfQR.Contracts.Enums;
using HalfQR.Contracts.Requests;

namespace HalfQR.Contracts.Models;

public sealed record RenderJobState
{
    public Guid JobId { get; init; }

    public SubmitRenderJobRequest Request { get; init; } = new();

    // Credential that created the job. Null for anonymous jobs. Status and artifacts are only served to the same credential.
    public Guid? OwnerCredentialId { get; init; }

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