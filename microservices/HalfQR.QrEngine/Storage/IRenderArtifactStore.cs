using HalfQR.Contracts.Models;

namespace HalfQR.QrEngine.Storage;

public interface IRenderArtifactStore
{
    Task<RenderArtifactState> SaveAsync(Guid jobId, string format, string contentType, byte[] content, CancellationToken cancellationToken);

    Task<(byte[] Content, string ContentType)?> GetAsync(RenderArtifactState artifact, CancellationToken cancellationToken);
}