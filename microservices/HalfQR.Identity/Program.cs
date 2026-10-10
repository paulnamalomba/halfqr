using System.Net;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using HalfQR.Contracts.Security;
using HalfQR.Identity;
using HalfQR.Identity.Email;
using HalfQR.Identity.Endpoints;
using HalfQR.Identity.Google;
using HalfQR.Identity.Security;
using HalfQR.Identity.Storage;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<IdentityOptions>(builder.Configuration.GetSection("Identity"));
builder.Services.Configure<GoogleOAuthOptions>(builder.Configuration.GetSection("GoogleOAuth"));
builder.Services.Configure<ResendOptions>(builder.Configuration.GetSection("Resend"));
builder.Services.ConfigureHttpJsonOptions(options =>
{
	options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
});
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 64 * 1024);

// Identity is only reachable through the webapp server, which forwards the browser's address.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
	options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
	options.ForwardLimit = 1;
	options.KnownIPNetworks.Clear();
	options.KnownProxies.Clear();
});

builder.Services.AddSingleton(TimeProvider.System);

if (string.Equals(builder.Configuration["Identity:StoreProvider"], IdentityOptions.PostgreSqlProvider, StringComparison.OrdinalIgnoreCase))
{
	builder.Services.AddSingleton<IIdentityStore, PostgresIdentityStore>();
}
else
{
	builder.Services.AddSingleton<IIdentityStore, FileSystemIdentityStore>();
}

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<EmailDomainPolicy>();
builder.Services.AddSingleton<UserDirectory>();
builder.Services.AddSingleton<SessionService>();
builder.Services.AddSingleton<CredentialService>();
builder.Services.AddSingleton<EmailVerificationService>();
builder.Services.AddHttpClient<GoogleOAuthClient>(client => client.Timeout = TimeSpan.FromSeconds(10));
builder.Services.AddHttpClient<BillingClient>(client => client.Timeout = TimeSpan.FromSeconds(10));
builder.Services.AddHttpClient<ResendEmailSender>(client => client.Timeout = TimeSpan.FromSeconds(10));

// Without a Resend key, Development logs sign-in links instead of sending them. Other environments report email as unavailable.
if (string.IsNullOrWhiteSpace(builder.Configuration["Resend:ApiKey"]) && builder.Environment.IsDevelopment())
{
	builder.Services.AddSingleton<IEmailSender, LoggingEmailSender>();
}
else
{
	builder.Services.AddSingleton<IEmailSender>(services => services.GetRequiredService<ResendEmailSender>());
}

builder.Services.AddRateLimiter(options =>
{
	options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
	options.AddPolicy("sign-in", context => RateLimitPartition.GetFixedWindowLimiter(
		ClientAddress(context),
		_ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
	options.AddPolicy("account", context => RateLimitPartition.GetFixedWindowLimiter(
		ClientAddress(context),
		_ => new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1) }));
});

var app = builder.Build();

app.UseForwardedHeaders();
app.Use(async (context, next) =>
{
	context.Response.Headers.XContentTypeOptions = "nosniff";
	context.Response.Headers["Referrer-Policy"] = "no-referrer";
	context.Response.Headers.CacheControl = "no-store";
	await next();
});
app.UseRateLimiter();

app.MapGet("/", () => Results.Ok(new { service = "HalfQR.Identity", status = "ok", version = "0.5.0.0" }));
app.MapGet("/healthz", () => Results.Ok(new { status = "healthy" }));

var auth = app.MapGroup("/api/v1/auth").RequireRateLimiting("sign-in");

// Read on every sign-in page render, so it uses the general limit rather than the strict sign-in one.
app.MapGet("/api/v1/auth/config", (IOptions<GoogleOAuthOptions> google, IOptions<IdentityOptions> identity, IEmailSender emailSender) =>
	Results.Ok(new AuthConfigResponse(
		google.Value.IsConfigured,
		google.Value.IsConfigured ? google.Value.ClientId : null,
		emailSender.IsConfigured,
		identity.Value.EnableDevSignIn)))
	.RequireRateLimiting("account");

auth.MapPost("/google/exchange", async (
	GoogleExchangeRequest request,
	HttpContext httpContext,
	GoogleOAuthClient google,
	EmailDomainPolicy emailPolicy,
	UserDirectory users,
	SessionService sessions,
	CancellationToken cancellationToken) =>
{
	if (!google.IsConfigured)
	{
		return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Google sign-in is not configured yet.", type: "google_oauth_not_configured");
	}

	if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.CodeVerifier) || !google.IsAllowedRedirectUri(request.RedirectUri))
	{
		return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid Google sign-in request.");
	}

	try
	{
		var identity = await google.ExchangeCodeAsync(request.Code, request.RedirectUri, request.CodeVerifier, cancellationToken);
		var rejection = emailPolicy.Evaluate(identity.Email);

		if (rejection != EmailRejection.None)
		{
			return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: EmailDomainPolicy.Describe(rejection), type: "email_domain_blocked");
		}

		var user = await users.UpsertAsync("google", identity.Subject, EmailDomainPolicy.Normalize(identity.Email), identity.DisplayName, identity.AvatarUrl, cancellationToken);
		return Results.Ok(await IssueSessionAsync(sessions, user, httpContext, cancellationToken));
	}
	catch (GoogleOAuthException exception)
	{
		app.Logger.LogWarning("Google sign-in failed: {Reason}", exception.Message);
		return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Google sign-in failed.");
	}
});

auth.MapPost("/dev", async (
	DevSignInRequest request,
	HttpContext httpContext,
	IOptions<IdentityOptions> identity,
	EmailDomainPolicy emailPolicy,
	UserDirectory users,
	SessionService sessions,
	CancellationToken cancellationToken) =>
{
	if (!identity.Value.EnableDevSignIn)
	{
		return Results.NotFound();
	}

	var email = EmailDomainPolicy.Normalize(request.Email);
	var rejection = emailPolicy.Evaluate(email);

	if (rejection != EmailRejection.None)
	{
		return Results.ValidationProblem(new Dictionary<string, string[]> { ["email"] = [EmailDomainPolicy.Describe(rejection)] });
	}

	var displayName = string.IsNullOrWhiteSpace(request.DisplayName) ? email.Split('@')[0] : request.DisplayName.Trim()[..Math.Min(request.DisplayName.Trim().Length, 80)];
	var user = await users.UpsertAsync("dev", email, email, displayName, null, cancellationToken);
	return Results.Ok(await IssueSessionAsync(sessions, user, httpContext, cancellationToken));
});

// Passwordless email sign-in and sign-up. The link is sent through Resend and verifies the address.
auth.MapPost("/email/start", async (EmailSignInRequest request, EmailDomainPolicy emailPolicy, EmailVerificationService verification, CancellationToken cancellationToken) =>
{
	var email = EmailDomainPolicy.Normalize(request.Email);
	var rejection = emailPolicy.Evaluate(email);

	if (rejection != EmailRejection.None)
	{
		return Results.ValidationProblem(new Dictionary<string, string[]> { ["email"] = [EmailDomainPolicy.Describe(rejection)] });
	}

	return await verification.SendSignInLinkAsync(email, cancellationToken) switch
	{
		EmailLinkResult.Sent => Results.Accepted(value: new { sent = true }),
		EmailLinkResult.Throttled => Results.Problem(statusCode: StatusCodes.Status429TooManyRequests, title: "A link was sent less than a minute ago. Check your inbox or try again shortly."),
		_ => Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Email sign-in is temporarily unavailable."),
	};
});

auth.MapPost("/email/verify", async (
	EmailVerifyRequest request,
	HttpContext httpContext,
	EmailVerificationService verification,
	EmailDomainPolicy emailPolicy,
	UserDirectory users,
	SessionService sessions,
	CancellationToken cancellationToken) =>
{
	var email = await verification.ConsumeAsync(request.Token, cancellationToken);

	if (email is null)
	{
		return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "This sign-in link is invalid, already used or expired.", type: "email_link_invalid");
	}

	// Lists can change between sending and clicking, so the domain is checked again.
	if (emailPolicy.Evaluate(email) is var rejection && rejection != EmailRejection.None)
	{
		return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: EmailDomainPolicy.Describe(rejection), type: "email_domain_blocked");
	}

	var user = await users.UpsertAsync("email", email, email, email.Split('@')[0], null, cancellationToken);
	return Results.Ok(await IssueSessionAsync(sessions, user, httpContext, cancellationToken));
});

var account = app.MapGroup("/api/v1")
	.AddEndpointFilter<SessionAuthenticationFilter>()
	.RequireRateLimiting("account");

account.MapGet("/me", (HttpContext httpContext) => Results.Ok(UserResponse.From(httpContext.GetSession().User)));

account.MapPost("/auth/sign-out", async (HttpContext httpContext, SessionService sessions, CancellationToken cancellationToken) =>
{
	var current = httpContext.GetSession();
	await sessions.RevokeAsync(current.User.Id, current.Session.Id, cancellationToken);
	return Results.NoContent();
});

account.MapGet("/sessions", async (HttpContext httpContext, IIdentityStore store, TimeProvider timeProvider, CancellationToken cancellationToken) =>
{
	var current = httpContext.GetSession();
	var now = timeProvider.GetUtcNow();
	var sessions = await store.ListSessionsAsync(current.User.Id, cancellationToken);

	return Results.Ok(sessions
		.Where(session => session.IsActive(now))
		.OrderByDescending(static session => session.LastSeenAt)
		.Select(session => SessionResponse.From(session, current.Session.Id, now)));
});

account.MapDelete("/sessions/{sessionId:guid}", async (Guid sessionId, HttpContext httpContext, SessionService sessions, CancellationToken cancellationToken) =>
	await sessions.RevokeAsync(httpContext.GetSession().User.Id, sessionId, cancellationToken)
		? Results.NoContent()
		: Results.NotFound());

account.MapGet("/credentials", async (HttpContext httpContext, CredentialService credentials, TimeProvider timeProvider, CancellationToken cancellationToken) =>
{
	var now = timeProvider.GetUtcNow();
	var items = await credentials.ListAsync(httpContext.GetSession().User.Id, cancellationToken);
	return Results.Ok(items.Select(credential => CredentialResponse.From(credential, now)));
});

account.MapGet("/credentials/options", () => Results.Ok(new
{
	scopes = ApiScopes.All,
	accessTokenLifetimesDays = CredentialService.AccessTokenLifetimesDays,
	apiKeyLifetimesDays = CredentialService.ApiKeyLifetimesDays,
}));

account.MapPost("/credentials", async (
	CreateCredentialRequest request,
	HttpContext httpContext,
	CredentialService credentials,
	TimeProvider timeProvider,
	CancellationToken cancellationToken) =>
{
	var errors = CredentialService.Validate(request.Kind, request.Name, request.Scopes, request.ExpiresInDays);

	if (errors.Count > 0)
	{
		return Results.ValidationProblem(errors);
	}

	var (error, secret, credential) = await credentials.CreateAsync(httpContext.GetSession().User.Id, request.Kind, request.Name, request.Scopes!, request.ExpiresInDays, cancellationToken);

	return error == CredentialIssueError.None
		? Results.Ok(new CredentialCreatedResponse(CredentialResponse.From(credential!, timeProvider.GetUtcNow()), secret!))
		: CredentialIssueProblem(error);
});

account.MapPost("/credentials/{credentialId:guid}/rotate", async (
	Guid credentialId,
	HttpContext httpContext,
	CredentialService credentials,
	TimeProvider timeProvider,
	CancellationToken cancellationToken) =>
{
	if (await credentials.RotateAsync(httpContext.GetSession().User.Id, credentialId, cancellationToken) is not { } rotated)
	{
		return Results.NotFound();
	}

	return rotated.Error == CredentialIssueError.None
		? Results.Ok(new CredentialCreatedResponse(CredentialResponse.From(rotated.Credential!, timeProvider.GetUtcNow()), rotated.Secret!))
		: CredentialIssueProblem(rotated.Error);
});

// Billing for the signed-in user, relayed to HalfQR.Billing with the user id taken from the session.
account.MapGet("/billing", async (HttpContext httpContext, BillingClient billing, CancellationToken cancellationToken) =>
	await billing.ForwardAsync(HttpMethod.Get, $"/internal/v1/users/{httpContext.GetSession().User.Id}/overview", null, cancellationToken));

account.MapPost("/billing/checkout", async (CheckoutRequest request, HttpContext httpContext, BillingClient billing, CancellationToken cancellationToken) =>
	await billing.ForwardAsync(HttpMethod.Post, $"/internal/v1/users/{httpContext.GetSession().User.Id}/checkout", request, cancellationToken));

account.MapPost("/billing/payments/{paymentId:guid}/refresh", async (Guid paymentId, HttpContext httpContext, BillingClient billing, CancellationToken cancellationToken) =>
{
	var userId = httpContext.GetSession().User.Id;
	billing.InvalidateEntitlement(userId);
	return await billing.ForwardAsync(HttpMethod.Post, $"/internal/v1/users/{userId}/payments/{paymentId}/refresh", null, cancellationToken);
});

account.MapDelete("/credentials/{credentialId:guid}", async (Guid credentialId, HttpContext httpContext, CredentialService credentials, CancellationToken cancellationToken) =>
	await credentials.RevokeAsync(httpContext.GetSession().User.Id, credentialId, cancellationToken) is null
		? Results.NotFound()
		: Results.NoContent());

// Service-to-service. PublicApi verifies API keys and access tokens here.
app.MapPost("/internal/v1/credentials/verify", async (
	CredentialVerificationRequest request,
	HttpContext httpContext,
	IOptions<IdentityOptions> identity,
	CredentialService credentials,
	CancellationToken cancellationToken) =>
{
	var expectedKey = identity.Value.InternalApiKey;
	var providedKey = httpContext.Request.Headers[InternalHeaders.InternalKey].ToString();

	if (string.IsNullOrEmpty(expectedKey) || !SecretTokens.FixedTimeEquals(expectedKey, providedKey))
	{
		return Results.NotFound();
	}

	return Results.Ok(await credentials.VerifyAsync(request.Token, cancellationToken));
});

app.Run();

static async Task<SessionIssuedResponse> IssueSessionAsync(SessionService sessions, HalfQR.Identity.Models.UserAccount user, HttpContext httpContext, CancellationToken cancellationToken)
{
	var (token, session) = await sessions.CreateAsync(
		user.Id,
		httpContext.Request.Headers.UserAgent.ToString(),
		httpContext.Connection.RemoteIpAddress?.ToString(),
		cancellationToken);

	return new SessionIssuedResponse(token, session.ExpiresAt, UserResponse.From(user));
}

static IResult CredentialIssueProblem(CredentialIssueError error)
	=> error switch
	{
		CredentialIssueError.PaymentRequired => Results.Problem(statusCode: StatusCodes.Status402PaymentRequired, title: "API keys and access tokens need an active paid plan.", type: "payment_required"),
		CredentialIssueError.LimitReached => Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Your plan's limit for this credential type is reached. Revoke an unused one or upgrade.", type: "credential_limit_reached"),
		_ => Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Billing is temporarily unavailable. Try again shortly.", type: "billing_unavailable"),
	};

static string ClientAddress(HttpContext context)
	=> context.Connection.RemoteIpAddress?.ToString() ?? IPAddress.None.ToString();
