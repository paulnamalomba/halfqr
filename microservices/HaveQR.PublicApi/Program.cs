using HaveQR.Contracts.Enums;
using HaveQR.Contracts.Options;
using HaveQR.Contracts.Requests;
using HaveQR.Contracts.Responses;
using HaveQR.PublicApi.Services;
using HaveQR.QrEngine.Hashing;
using HaveQR.QrEngine.PayloadEncoding;
using HaveQR.QrEngine.Storage;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection("RabbitMq"));
builder.Services.Configure<RenderStorageOptions>(builder.Configuration.GetSection("RenderStorage"));
builder.Services.AddSingleton<IQrPayloadEncoder, QrPayloadEncoder>();
builder.Services.AddSingleton<IHashService, Sha256HashService>();
builder.Services.AddSingleton<IRenderJobStore, FileSystemRenderJobStore>();
builder.Services.AddSingleton<RabbitMqJobDispatcher>();
builder.Services.AddSingleton<RenderJobService>();

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
	service = "HaveQR.PublicApi",
	status = "ok",
	version = "0.2.0.0",
}));

app.MapGet("/healthz", () => Results.Ok(new { status = "healthy" }));

app.MapGet("/api/v1/qr/content-types", () => Results.Ok(Enum.GetNames<QrContentType>()));

app.MapPost("/api/v1/qr/render", async (
	SubmitRenderJobRequest request,
	RenderJobService renderJobs,
	CancellationToken cancellationToken) =>
{
	var response = await renderJobs.EnqueueAsync(request, cancellationToken);
	var statusUrl = response.StatusUrl;
	return Results.Accepted(statusUrl, response);
});

app.MapGet("/api/v1/qr/jobs/{jobId:guid}", async (
	Guid jobId,
	RenderJobService renderJobs,
	CancellationToken cancellationToken) =>
{
	var job = await renderJobs.GetStatusAsync(jobId, cancellationToken);
	return job is null
		? Results.NotFound()
		: Results.Ok(job);
});

app.MapGet("/api/v1/qr/jobs/{jobId:guid}/artifacts/{format}", async (
	Guid jobId,
	string format,
	RenderJobService renderJobs,
	CancellationToken cancellationToken) =>
{
	var artifact = await renderJobs.GetArtifactAsync(jobId, format, cancellationToken);
	return artifact is null
		? Results.NotFound()
		: Results.File(artifact.Value.Content, artifact.Value.ContentType, fileDownloadName: $"haveqr-{jobId:N}.{format}");
});

app.Run();
