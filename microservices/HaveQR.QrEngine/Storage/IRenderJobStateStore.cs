using HaveQR.Contracts.Models;

namespace HaveQR.QrEngine.Storage;

public interface IRenderJobStateStore
{
    Task SaveAsync(RenderJobState state, CancellationToken cancellationToken);

    Task<RenderJobState?> GetAsync(Guid jobId, CancellationToken cancellationToken);
}