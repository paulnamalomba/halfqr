using HaveQR.Contracts.Models;

namespace HaveQR.QrEngine.Storage;

public sealed class CompositeRenderJobStore(
    IRenderJobStateStore stateStore,
    IRenderArtifactStore artifactStore) : IRenderJobStore
{
    public Task SaveAsync(RenderJobState state, CancellationToken cancellationToken)
        => stateStore.SaveAsync(state, cancellationToken);

    public Task<RenderJobState?> GetAsync(Guid jobId, CancellationToken cancellationToken)
        => stateStore.GetAsync(jobId, cancellationToken);

    public Task<RenderArtifactState> SaveArtifactAsync(Guid jobId, string format, string contentType, byte[] content, CancellationToken cancellationToken)
        => artifactStore.SaveAsync(jobId, format, contentType, content, cancellationToken);

    public async Task<(byte[] Content, string ContentType)?> GetArtifactAsync(Guid jobId, string format, CancellationToken cancellationToken)
    {
        var state = await stateStore.GetAsync(jobId, cancellationToken);
        var artifact = state?.Artifacts.FirstOrDefault(existing => existing.Format.Equals(format, StringComparison.OrdinalIgnoreCase));

        return artifact is null
            ? null
            : await artifactStore.GetAsync(artifact, cancellationToken);
    }
}