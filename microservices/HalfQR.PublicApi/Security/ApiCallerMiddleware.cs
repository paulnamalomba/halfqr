using System.Security.Cryptography;
using System.Text;
using HalfQR.Contracts.Security;
using Microsoft.Extensions.Options;

namespace HalfQR.PublicApi.Security;

// Accepts "Authorization: Bearer hqr_sk_..." / "hqr_pat_..." or "X-Api-Key". A supplied but invalid credential is always rejected.
public sealed class ApiCallerMiddleware(RequestDelegate next, IOptions<ApiAccessOptions> options)
{
    public const string ApiKeyHeader = "X-Api-Key";

    private readonly ApiAccessOptions _options = options.Value;

    public async Task InvokeAsync(HttpContext context, CredentialVerifier verifier)
    {
        var clientAddress = ResolveClientAddress(context);
        var token = ReadToken(context.Request);

        if (token is null)
        {
            context.Items[ApiCaller.ItemKey] = ApiCaller.Anonymous(clientAddress);
            await next(context);
            return;
        }

        var verification = await verifier.VerifyAsync(token, context.RequestAborted);

        if (!verification.Valid)
        {
            context.Response.Headers.WWWAuthenticate = "Bearer";
            await Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid, expired or revoked API credential.").ExecuteAsync(context);
            return;
        }

        context.Items[ApiCaller.ItemKey] = ApiCaller.FromCredential(verification, clientAddress);
        await next(context);
    }

    private static string? ReadToken(HttpRequest request)
    {
        var authorization = request.Headers.Authorization.ToString();

        if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return authorization["Bearer ".Length..].Trim() is { Length: > 0 } bearer ? bearer : null;
        }

        return request.Headers[ApiKeyHeader].ToString().Trim() is { Length: > 0 } apiKey ? apiKey : null;
    }

    private string ResolveClientAddress(HttpContext context)
    {
        var remoteAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        if (string.IsNullOrEmpty(_options.ProxySecret))
        {
            return remoteAddress;
        }

        var providedSecret = context.Request.Headers[InternalHeaders.ProxySecret].ToString();

        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(providedSecret), Encoding.UTF8.GetBytes(_options.ProxySecret)))
        {
            return remoteAddress;
        }

        var forwarded = context.Request.Headers["X-Forwarded-For"].ToString().Split(',')[0].Trim();
        return forwarded.Length > 0 ? forwarded : remoteAddress;
    }
}

public static class ScopeEndpointExtensions
{
    // Anonymous callers pass when API keys are optional; credentialed callers must hold the scope.
    public static RouteHandlerBuilder RequireApiScope(this RouteHandlerBuilder builder, string scope)
        => builder.AddEndpointFilter(async (context, next) =>
        {
            var caller = context.HttpContext.GetCaller();
            var requireApiKey = context.HttpContext.RequestServices.GetRequiredService<IOptions<ApiAccessOptions>>().Value.RequireApiKey;

            if (caller.IsAnonymous && requireApiKey)
            {
                context.HttpContext.Response.Headers.WWWAuthenticate = "Bearer";
                return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "An API key or access token is required.");
            }

            if (!caller.IsAnonymous && !caller.HasScope(scope))
            {
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: $"This credential is missing the '{scope}' scope.");
            }

            return await next(context);
        });
}
