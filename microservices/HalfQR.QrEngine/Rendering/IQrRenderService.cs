using HalfQR.Contracts.Requests;

namespace HalfQR.QrEngine.Rendering;

public interface IQrRenderService
{
    Task<QrDraftRenderResult> RenderDraftAsync(SubmitRenderJobRequest request, CancellationToken cancellationToken);

    Task<QrRenderArtifacts> RenderAsync(SubmitRenderJobRequest request, CancellationToken cancellationToken);
}