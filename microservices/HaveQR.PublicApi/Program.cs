using HaveQR.Contracts.Enums;
using HaveQR.Contracts.Requests;
using HaveQR.Contracts.Responses;
using HaveQR.PublicApi.Services;
using HaveQR.QrEngine.Hashing;
using HaveQR.QrEngine.PayloadEncoding;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IQrPayloadEncoder, QrPayloadEncoder>();
builder.Services.AddSingleton<IHashService, Sha256HashService>();
builder.Services.AddSingleton<InMemoryRenderJobService>();
builder.Services.AddHostedService<InMemoryRenderJobProcessor>();

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
	service = "HaveQR.PublicApi",
	status = "ok",
	version = "0.1.0.0",
}));

app.MapGet("/healthz", () => Results.Ok(new { status = "healthy" }));

app.MapGet("/api/v1/qr/content-types", () => Results.Ok(Enum.GetNames<QrContentType>()));

app.MapPost("/api/v1/qr/render", async (
	SubmitRenderJobRequest request,
	InMemoryRenderJobService renderJobs,
	CancellationToken cancellationToken) =>
{
	var jobId = await renderJobs.EnqueueAsync(request, cancellationToken);
	var statusUrl = $"/api/v1/qr/jobs/{jobId}";
	var response = new RenderJobAcceptedResponse(jobId, QrJobStatus.Queued, statusUrl);
	return Results.Accepted(statusUrl, response);
});

app.MapGet("/api/v1/qr/jobs/{jobId:guid}", (
	Guid jobId,
	InMemoryRenderJobService renderJobs) =>
{
	var job = renderJobs.GetStatus(jobId);
	return job is null
		? Results.NotFound()
		: Results.Ok(job);
});

app.Run();
