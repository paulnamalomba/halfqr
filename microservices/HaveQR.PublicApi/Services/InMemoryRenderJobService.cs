using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using HaveQR.Contracts.Enums;
using HaveQR.Contracts.Requests;
using HaveQR.Contracts.Responses;
using HaveQR.QrEngine.Hashing;
using HaveQR.QrEngine.PayloadEncoding;

namespace HaveQR.PublicApi.Services;

internal sealed class InMemoryRenderJobService(
    IQrPayloadEncoder payloadEncoder,
    IHashService hashService,
    ILogger<InMemoryRenderJobService> logger)
{
    private readonly ConcurrentDictionary<Guid, RenderJobDocument> _jobs = new();
    private readonly Channel<Guid> _jobQueue = Channel.CreateUnbounded<Guid>();

    public async ValueTask<Guid> EnqueueAsync(SubmitRenderJobRequest request, CancellationToken cancellationToken)
    {
        var jobId = Guid.NewGuid();
        var job = new RenderJobDocument
        {
            JobId = jobId,
            Request = request,
            Status = QrJobStatus.Queued,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        _jobs[jobId] = job;
        await _jobQueue.Writer.WriteAsync(jobId, cancellationToken);
        return jobId;
    }

    public RenderJobStatusResponse? GetStatus(Guid jobId)
        => _jobs.TryGetValue(jobId, out var job) ? job.ToResponse() : null;

    public async Task RunAsync(CancellationToken stoppingToken)
    {
        while (await _jobQueue.Reader.WaitToReadAsync(stoppingToken))
        {
            while (_jobQueue.Reader.TryRead(out var jobId))
            {
                if (!_jobs.TryGetValue(jobId, out var job))
                {
                    continue;
                }

                await ProcessAsync(job, stoppingToken);
            }
        }
    }

    private async Task ProcessAsync(RenderJobDocument job, CancellationToken cancellationToken)
    {
        job.Status = QrJobStatus.Running;
        job.StartedAt = DateTimeOffset.UtcNow;

        try
        {
            var encodedPayload = payloadEncoder.Encode(job.Request.ContentType, job.Request.Payload);
            var canonicalRequest = SerializeCanonicalRequest(job.Request);

            job.EncodedPayload = encodedPayload;
            job.ConfigurationHash = hashService.Compute(canonicalRequest);
            job.PayloadHash = hashService.Compute(encodedPayload);

            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);

            job.Status = QrJobStatus.Completed;
            job.CompletedAt = DateTimeOffset.UtcNow;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Render job {JobId} failed during bootstrap processing.", job.JobId);
            job.Status = QrJobStatus.Failed;
            job.CompletedAt = DateTimeOffset.UtcNow;
            job.FailureReason = exception.Message;
        }
    }

    private static string SerializeCanonicalRequest(SubmitRenderJobRequest request)
    {
        var canonical = new
        {
            ContentType = request.ContentType.ToString(),
            Mode = request.Mode.ToString(),
            Payload = request.Payload
                .OrderBy(static entry => entry.Key, StringComparer.Ordinal)
                .ToDictionary(static entry => entry.Key, static entry => entry.Value, StringComparer.Ordinal),
            Output = request.Output,
            Finder = request.Finder,
            Colors = request.Colors,
            Logo = request.Logo is null
                ? null
                : new
                {
                    HasSvg = !string.IsNullOrWhiteSpace(request.Logo.Svg),
                    request.Logo.SizePercent,
                },
        };

        return JsonSerializer.Serialize(canonical);
    }

    private sealed class RenderJobDocument
    {
        public Guid JobId { get; init; }

        public required SubmitRenderJobRequest Request { get; init; }

        public required QrJobStatus Status { get; set; }

        public required DateTimeOffset CreatedAt { get; init; }

        public DateTimeOffset? StartedAt { get; set; }

        public DateTimeOffset? CompletedAt { get; set; }

        public string? EncodedPayload { get; set; }

        public string? ConfigurationHash { get; set; }

        public string? PayloadHash { get; set; }

        public string? FailureReason { get; set; }

        public RenderJobStatusResponse ToResponse()
            => new()
            {
                JobId = JobId,
                Status = Status,
                ContentType = Request.ContentType,
                EncodedPayload = EncodedPayload,
                ConfigurationHash = ConfigurationHash,
                PayloadHash = PayloadHash,
                CreatedAt = CreatedAt,
                StartedAt = StartedAt,
                CompletedAt = CompletedAt,
                FailureReason = FailureReason,
            };
    }
}