using HaveQR.Contracts.Requests;

namespace HaveQR.QrEngine.Rendering;

public interface IQrRenderService
{
    Task<QrRenderArtifacts> RenderAsync(SubmitRenderJobRequest request, CancellationToken cancellationToken);
}