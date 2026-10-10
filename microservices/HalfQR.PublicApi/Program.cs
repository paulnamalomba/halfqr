using HalfQR.Contracts.Enums;
using HalfQR.Contracts.Options;
using HalfQR.Contracts.Requests;
using HalfQR.Contracts.Responses;
using HalfQR.Contracts.Security;
using HalfQR.PublicApi.Security;
using HalfQR.PublicApi.Services;
using HalfQR.QrEngine.Hashing;
using HalfQR.QrEngine.PayloadEncoding;
using HalfQR.QrEngine.Rendering;
using HalfQR.QrEngine.Storage;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
var allowedOrigins = builder.Configuration
	.GetSection("Cors:AllowedOrigins")
	.Get<string[]>()?
	.Where(origin => !string.IsNullOrWhiteSpace(origin))
	.Distinct(StringComparer.OrdinalIgnoreCase)
	.ToArray()
	??
	[
		"https://www.halfqr.com",
		"https://halfqr.com",
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
			.WithHeaders("Content-Type", "Authorization", ApiCallerMiddleware.ApiKeyHeader)
			.WithMethods("GET", "POST");
	});
});
builder.Services.ConfigureHttpJsonOptions(options =>
{
	options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
});

// Largest legitimate body is a 512 KB raster logo as base64 (~700 KB) plus options.
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 1024 * 1024);

builder.Services.Configure<ApiAccessOptions>(builder.Configuration.GetSection("ApiAccess"));
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<CredentialVerifier>((services, client) =>
{
	client.BaseAddress = new Uri(services.GetRequiredService<IOptions<ApiAccessOptions>>().Value.IdentityBaseUrl);
	client.Timeout = TimeSpan.FromSeconds(5);
});
builder.Services.AddRateLimiter(options =>
{
	options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
	options.AddPolicy("render", context => CreateCallerLimiter(context, static access => access.AnonymousRequestsPerMinute));
	options.AddPolicy("draft", context => CreateCallerLimiter(context, static access => access.AnonymousDraftRequestsPerMinute));
	options.AddPolicy("read", context => CreateCallerLimiter(context, static access => access.AnonymousRequestsPerMinute * 4));
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

app.Use(async (context, next) =>
{
	context.Response.Headers.XContentTypeOptions = "nosniff";
	context.Response.Headers["Referrer-Policy"] = "no-referrer";

	if (context.Request.IsHttps)
	{
		context.Response.Headers.StrictTransportSecurity = "max-age=31536000; includeSubDomains";
	}

	await next();
});
app.UseCors("HalfQrWebapp");
app.UseMiddleware<ApiCallerMiddleware>();
app.UseRateLimiter();

app.MapGet("/", () => Results.Ok(new
{
	service = "HalfQR.PublicApi",
	status = "ok",
	version = "0.4.0.0",
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
})
	.RequireApiScope(ApiScopes.QrRender)
	.RequireRateLimiting("draft");

app.MapPost("/api/v1/qr/render", async (
	SubmitRenderJobRequest request,
	HttpContext httpContext,
	RenderJobService renderJobs,
	CancellationToken cancellationToken) =>
{
	var validationErrors = QrRenderRequestValidator.Validate(request);

	if (validationErrors.Count > 0)
	{
		return Results.ValidationProblem(validationErrors);
	}

	var response = await renderJobs.EnqueueAsync(request, httpContext.GetCaller().CredentialId, cancellationToken);
	return Results.Accepted(response.StatusUrl, response);
})
	.RequireApiScope(ApiScopes.QrRender)
	.RequireRateLimiting("render");

app.MapGet("/api/v1/qr/jobs/{jobId:guid}", async (
	Guid jobId,
	HttpContext httpContext,
	RenderJobService renderJobs,
	CancellationToken cancellationToken) =>
{
	var job = await renderJobs.GetStatusAsync(jobId, httpContext.GetCaller().CredentialId, cancellationToken);
	return job is null
		? Results.NotFound()
		: Results.Ok(job);
})
	.RequireApiScope(ApiScopes.QrRead)
	.RequireRateLimiting("read");

app.MapGet("/api/v1/qr/jobs/{jobId:guid}/artifacts/{format}", async (
	Guid jobId,
	string format,
	HttpContext httpContext,
	RenderJobService renderJobs,
	CancellationToken cancellationToken) =>
{
	var artifact = await renderJobs.GetArtifactAsync(jobId, format, httpContext.GetCaller().CredentialId, cancellationToken);

	if (artifact is null)
	{
		return Results.NotFound();
	}

	// SVG artifacts are served from the API origin, so block script execution if one is opened directly.
	httpContext.Response.Headers.ContentSecurityPolicy = "default-src 'none'; img-src data:; style-src 'unsafe-inline'; sandbox";
	return Results.File(artifact.Value.Content, artifact.Value.ContentType, fileDownloadName: $"halfqr-{jobId:N}.{format}");
})
	.RequireApiScope(ApiScopes.QrRead)
	.RequireRateLimiting("read");

app.Run();

// Keyed callers get their plan's limit per credential; anonymous callers are limited per client address.
static RateLimitPartition<string> CreateCallerLimiter(HttpContext context, Func<ApiAccessOptions, int> anonymousLimit)
{
	var access = context.RequestServices.GetRequiredService<IOptions<ApiAccessOptions>>().Value;
	var caller = context.GetCaller();
	var permitLimit = caller.IsAnonymous ? anonymousLimit(access) : caller.RequestsPerMinute ?? access.KeyedRequestsPerMinute;

	return RateLimitPartition.GetFixedWindowLimiter(
		$"{caller.PartitionKey}:{permitLimit}",
		_ => new FixedWindowRateLimiterOptions { PermitLimit = permitLimit, Window = TimeSpan.FromMinutes(1) });
}
