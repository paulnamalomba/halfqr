using System.Text.Json.Serialization;
using System.Security.Cryptography;
using System.Text;
using HalfQR.Billing;
using HalfQR.Billing.Payments;
using HalfQR.Billing.PayChangu;
using HalfQR.Billing.Storage;
using HalfQR.Contracts.Security;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<BillingOptions>(builder.Configuration.GetSection("Billing"));
builder.Services.Configure<PayChanguOptions>(builder.Configuration.GetSection("PayChangu"));
builder.Services.ConfigureHttpJsonOptions(options =>
{
	options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
});
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 64 * 1024);

builder.Services.AddSingleton(TimeProvider.System);

if (string.Equals(builder.Configuration["Billing:StoreProvider"], BillingOptions.PostgreSqlProvider, StringComparison.OrdinalIgnoreCase))
{
	builder.Services.AddSingleton<IBillingStore, PostgresBillingStore>();
}
else
{
	builder.Services.AddSingleton<IBillingStore, FileSystemBillingStore>();
}

builder.Services.AddHttpClient<PayChanguClient>(client => client.Timeout = TimeSpan.FromSeconds(20));
builder.Services.AddSingleton<BillingService>();
builder.Services.AddHostedService<PaymentTimeoutWorker>();

var app = builder.Build();

app.Use(async (context, next) =>
{
	context.Response.Headers.XContentTypeOptions = "nosniff";
	context.Response.Headers.CacheControl = "no-store";
	await next();
});

app.MapGet("/", () => Results.Ok(new { service = "HalfQR.Billing", status = "ok", version = "0.5.0.0" }));
app.MapGet("/healthz", () => Results.Ok(new { status = "healthy" }));

app.MapGet("/api/v1/billing/plans", (BillingService billing) => Results.Ok(billing.Plans.Select(PlanResponse.From)));

// Public. Signature is required in Production; the payment is always re-verified with PayChangu before activation.
app.MapPost("/api/v1/billing/webhooks/paychangu", async (HttpRequest request, BillingService billing, IOptions<PayChanguOptions> payChangu, IHostEnvironment environment, CancellationToken cancellationToken) =>
{
	using var reader = new StreamReader(request.Body, Encoding.UTF8);
	var rawBody = await reader.ReadToEndAsync(cancellationToken);
	var secret = payChangu.Value.WebhookSecret;

	if (string.IsNullOrWhiteSpace(secret))
	{
		if (environment.IsProduction())
		{
			app.Logger.LogError("PayChangu webhook rejected: PayChangu:WebhookSecret is not configured in Production.");
			return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
		}

		app.Logger.LogWarning("PayChangu webhook signature check skipped outside Production because no webhook secret is configured.");
	}
	else if (!PayChanguClient.IsValidSignature(rawBody, secret, request.Headers["Signature"].FirstOrDefault()))
	{
		app.Logger.LogWarning("PayChangu webhook signature validation failed.");
		return Results.Unauthorized();
	}

	await billing.ProcessWebhookAsync(rawBody, cancellationToken);

	// Always 200 for authenticated deliveries so PayChangu stops retrying; unknown references are logged.
	return Results.Ok(new { received = true });
});

// Service-to-service. Identity calls these on behalf of a signed-in user.
var internalApi = app.MapGroup("/internal/v1/users/{userId:guid}")
	.AddEndpointFilter(async (context, next) =>
	{
		var expected = context.HttpContext.RequestServices.GetRequiredService<IOptions<BillingOptions>>().Value.InternalApiKey;
		var provided = context.HttpContext.Request.Headers[InternalHeaders.InternalKey].ToString();

		return string.IsNullOrEmpty(expected) || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(provided))
			? Results.NotFound()
			: await next(context);
	});

internalApi.MapGet("/entitlement", async (Guid userId, BillingService billing, CancellationToken cancellationToken) =>
	Results.Ok(await billing.GetEntitlementAsync(userId, cancellationToken)));

internalApi.MapGet("/overview", async (Guid userId, BillingService billing, CancellationToken cancellationToken) =>
	Results.Ok(new BillingOverviewResponse(
		await billing.GetEntitlementAsync(userId, cancellationToken),
		billing.Plans.Select(PlanResponse.From).ToArray(),
		billing.Operators.Select(static entry => new OperatorResponse(entry.Key, entry.Value.Label, entry.Value.NumberPrefix)).ToArray(),
		(await billing.ListPaymentsAsync(userId, cancellationToken)).Select(PaymentResponse.From).ToArray(),
		billing.IsMockMode)));

internalApi.MapPost("/checkout", async (Guid userId, CheckoutRequest request, BillingService billing, CancellationToken cancellationToken) =>
{
	var result = await billing.StartCheckoutAsync(userId, request.PlanId, request.Operator, request.PhoneNumber, cancellationToken);

	if (result.Errors is not null)
	{
		return Results.ValidationProblem(result.Errors);
	}

	return result.Payment is null
		? Results.Problem(statusCode: StatusCodes.Status409Conflict, title: result.ProblemTitle)
		: Results.Ok(PaymentResponse.From(result.Payment));
});

internalApi.MapPost("/payments/{paymentId:guid}/refresh", async (Guid userId, Guid paymentId, BillingService billing, CancellationToken cancellationToken) =>
	await billing.RefreshAsync(paymentId, userId, cancellationToken) is { } payment
		? Results.Ok(PaymentResponse.From(payment))
		: Results.NotFound());

app.Run();
