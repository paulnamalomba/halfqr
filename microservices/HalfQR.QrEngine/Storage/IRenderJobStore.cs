using HalfQR.Contracts.Models;

namespace HalfQR.QrEngine.Storage;

public interface IRenderJobStore
{
    Task SaveAsync(RenderJobState state, CancellationToken cancellationToken);

    Task<RenderJobState?> GetAsync(Guid jobId, CancellationToken cancellationToken);

    Task<RenderArtifactState> SaveArtifactAsync(Guid jobId, string format, string contentType, byte[] content, CancellationToken cancellationToken);

    Task<(byte[] Content, string ContentType)?> GetArtifactAsync(Guid jobId, string format, CancellationToken cancellationToken);
}