using HalfQR.Contracts.Models;

namespace HalfQR.QrEngine.Storage;

public interface IRenderJobStateStore
{
    Task SaveAsync(RenderJobState state, CancellationToken cancellationToken);

    Task<RenderJobState?> GetAsync(Guid jobId, CancellationToken cancellationToken);
}