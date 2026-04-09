namespace HaveQR.PublicApi.Services;

internal sealed class InMemoryRenderJobProcessor(
    InMemoryRenderJobService renderJobs,
    ILogger<InMemoryRenderJobProcessor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("In-memory render job processor started.");
        await renderJobs.RunAsync(stoppingToken);
    }
}