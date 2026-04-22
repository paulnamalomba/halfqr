using HalfQR.Contracts.Enums;
using HalfQR.Contracts.Options;
using HalfQR.Contracts.Requests;
using HalfQR.Contracts.Responses;
using HalfQR.PublicApi.Services;
using HalfQR.QrEngine.Hashing;
using HalfQR.QrEngine.PayloadEncoding;
using HalfQR.QrEngine.Rendering;
using HalfQR.QrEngine.Storage;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
var allowedOrigins = builder.Configuration
	.GetSection("Cors:AllowedOrigins")
	.Get<string[]>()?
	.Where(origin => !string.IsNullOrWhiteSpace(origin))
	.Distinct(StringComparer.OrdinalIgnoreCase)
	.ToArray()
	??
	[
		"https://halfqr.computemore.com",
		"http://localhost:5173",
		"http://127.0.0.1:5173",
		"http://localhost:3000",
		"http://127.0.0.1:3000",
	];

builder.Services.AddCors(options =>
{
	options.AddPolicy("HalfQrWebapp", policy =>
	{
		policy
			.WithOrigins(allowedOrigins)
			.AllowAnyHeader()
			.AllowAnyMethod();
	});
});
builder.Services.ConfigureHttpJsonOptions(options =>
{
	options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection("RabbitMq"));
builder.Services.Configure<RenderStorageOptions>(builder.Configuration.GetSection("RenderStorage"));
builder.Services.Configure<PostgresRenderStoreOptions>(builder.Configuration.GetSection("PostgresRenderStore"));
builder.Services.Configure<R2StorageOptions>(builder.Configuration.GetSection("R2Storage"));
builder.Services.AddSingleton<IQrPayloadEncoder, QrPayloadEncoder>();
builder.Services.AddSingleton<IHashService, Sha256HashService>();
builder.Services.AddSingleton<IQrRenderService, QrRenderService>();

if (string.Equals(builder.Configuration["RenderStorage:JobStateProvider"], RenderStorageOptions.PostgreSqlProvider, StringComparison.OrdinalIgnoreCase))
{
	builder.Services.AddSingleton<IRenderJobStateStore, PostgresRenderJobStateStore>();
}
else
{
	builder.Services.AddSingleton<IRenderJobStateStore, FileSystemRenderJobStateStore>();
}

if (string.Equals(builder.Configuration["RenderStorage:ArtifactProvider"], RenderStorageOptions.R2Provider, StringComparison.OrdinalIgnoreCase))
{
	builder.Services.AddSingleton<IRenderArtifactStore, R2RenderArtifactStore>();
}
else
{
	builder.Services.AddSingleton<IRenderArtifactStore, FileSystemRenderArtifactStore>();
}

builder.Services.AddSingleton<IRenderJobStore, CompositeRenderJobStore>();
builder.Services.AddSingleton<RabbitMqJobDispatcher>();
builder.Services.AddSingleton<RenderJobService>();

var app = builder.Build();

app.UseCors("HalfQrWebapp");

app.MapGet("/", () => Results.Ok(new
{
	service = "HalfQR.PublicApi",
	status = "ok",
	version = "0.2.9.0",
}));

app.MapGet("/healthz", () => Results.Ok(new { status = "healthy" }));

app.MapGet("/api/v1/qr/content-types", () => Results.Ok(Enum.GetNames<QrContentType>()));

app.MapPost("/api/v1/qr/render/draft", async (
	SubmitRenderJobRequest request,
	IQrRenderService renderService,
	CancellationToken cancellationToken) =>
{
	var validationErrors = QrRenderRequestValidator.Validate(request);

	if (validationErrors.Count > 0)
	{
		return Results.ValidationProblem(validationErrors);
	}

	try
	{
		var preview = await renderService.RenderDraftAsync(request, cancellationToken);
		return Results.Ok(new RenderDraftPreviewResponse(
			preview.ResolvedTargetUrl,
			preview.EncodedPayload,
			preview.ConfigurationHash,
			preview.PayloadHash,
			preview.SvgMarkup));
	}
	catch (InvalidOperationException exception)
	{
		return Results.ValidationProblem(new Dictionary<string, string[]>
		{
			["request"] = [exception.Message],
		});
	}
});

app.MapPost("/api/v1/qr/render", async (
	SubmitRenderJobRequest request,
	RenderJobService renderJobs,
	CancellationToken cancellationToken) =>
{
	var validationErrors = QrRenderRequestValidator.Validate(request);

	if (validationErrors.Count > 0)
	{
		return Results.ValidationProblem(validationErrors);
	}

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
		: Results.File(artifact.Value.Content, artifact.Value.ContentType, fileDownloadName: $"halfqr-{jobId:N}.{format}");
});

app.Run();
