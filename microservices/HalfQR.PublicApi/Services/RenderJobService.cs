using HalfQR.Contracts.Enums;
using HalfQR.Contracts.Messages;
using HalfQR.Contracts.Models;
using HalfQR.Contracts.Requests;
using HalfQR.Contracts.Responses;
using HalfQR.QrEngine.Storage;

namespace HalfQR.PublicApi.Services;

internal sealed class RenderJobService(
    IRenderJobStore jobStore,
    RabbitMqJobDispatcher dispatcher)
{
    public async Task<RenderJobAcceptedResponse> EnqueueAsync(SubmitRenderJobRequest request, CancellationToken cancellationToken)
    {
        var jobId = Guid.NewGuid();
        var state = new RenderJobState
        {
            JobId = jobId,
            Request = request,
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
                FailureReason = $"Queue dispatch failed: {exception.Message}",
            };

            await jobStore.SaveAsync(state, CancellationToken.None);
            throw;
        }

        var statusUrl = $"/api/v1/qr/jobs/{jobId}";
        return new RenderJobAcceptedResponse(jobId, QrJobStatus.Queued, statusUrl);
    }

    public async Task<RenderJobStatusResponse?> GetStatusAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var state = await jobStore.GetAsync(jobId, cancellationToken);
        return state is null ? null : ToResponse(state, jobId);
    }

    public Task<(byte[] Content, string ContentType)?> GetArtifactAsync(Guid jobId, string format, CancellationToken cancellationToken)
        => jobStore.GetArtifactAsync(jobId, format, cancellationToken);

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