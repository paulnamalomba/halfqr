using HalfQR.Contracts.Enums;
using HalfQR.Contracts.Messages;
using HalfQR.Contracts.Models;
using HalfQR.Contracts.Requests;
using HalfQR.Contracts.Responses;
using HalfQR.QrEngine.Storage;

namespace HalfQR.PublicApi.Services;

internal sealed class RenderJobService(
    IRenderJobStore jobStore,
    RabbitMqJobDispatcher dispatcher,
    ILogger<RenderJobService> logger)
{
    public async Task<RenderJobAcceptedResponse> EnqueueAsync(SubmitRenderJobRequest request, Guid? ownerCredentialId, CancellationToken cancellationToken)
    {
        var jobId = Guid.NewGuid();
        var state = new RenderJobState
        {
            JobId = jobId,
            Request = request,
            OwnerCredentialId = ownerCredentialId,
            Status = QrJobStatus.Queued,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        await jobStore.SaveAsync(state, cancellationToken);

        try
        {
            await dispatcher.DispatchAsync(new RenderJobQueuedMessage(jobId), cancellationToken);
        }
        catch (Exception exception)
        {
            state = state with
            {
                Status = QrJobStatus.Failed,
                CompletedAt = DateTimeOffset.UtcNow,
                FailureReason = "The render queue is unavailable. Please retry shortly.",
            };

            await jobStore.SaveAsync(state, CancellationToken.None);
            logger.LogError(exception, "Queue dispatch failed for render job {JobId}.", jobId);
            throw;
        }

        var statusUrl = $"/api/v1/qr/jobs/{jobId}";
        return new RenderJobAcceptedResponse(jobId, QrJobStatus.Queued, statusUrl);
    }

    public async Task<RenderJobStatusResponse?> GetStatusAsync(Guid jobId, Guid? callerCredentialId, CancellationToken cancellationToken)
    {
        var state = await jobStore.GetAsync(jobId, cancellationToken);
        return state is null || !IsVisibleTo(state, callerCredentialId) ? null : ToResponse(state, jobId);
    }

    public async Task<(byte[] Content, string ContentType)?> GetArtifactAsync(Guid jobId, string format, Guid? callerCredentialId, CancellationToken cancellationToken)
    {
        var state = await jobStore.GetAsync(jobId, cancellationToken);

        return state is null || !IsVisibleTo(state, callerCredentialId)
            ? null
            : await jobStore.GetArtifactAsync(jobId, format, cancellationToken);
    }

    // Jobs created with a credential are private to it and reported as not found to anyone else.
    private static bool IsVisibleTo(RenderJobState state, Guid? callerCredentialId)
        => state.OwnerCredentialId is null || state.OwnerCredentialId == callerCredentialId;

    private static RenderJobStatusResponse ToResponse(RenderJobState state, Guid jobId)
        => new()
        {
            JobId = state.JobId,
            Status = state.Status,
            ContentType = state.Request.ContentType,
            EncodedPayload = state.EncodedPayload,
            ResolvedTargetUrl = state.ResolvedTargetUrl,
            ConfigurationHash = state.ConfigurationHash,
            PayloadHash = state.PayloadHash,
            CreatedAt = state.CreatedAt,
            StartedAt = state.StartedAt,
            CompletedAt = state.CompletedAt,
            FailureReason = state.FailureReason,
            Artifacts = state.Artifacts
                .Select(artifact => new RenderArtifactDescriptor
                {
                    Format = artifact.Format,
                    ContentType = artifact.ContentType,
                    SizeBytes = artifact.SizeBytes,
                    DownloadUrl = $"/api/v1/qr/jobs/{jobId}/artifacts/{artifact.Format}",
                })
                .ToArray(),
        };
}