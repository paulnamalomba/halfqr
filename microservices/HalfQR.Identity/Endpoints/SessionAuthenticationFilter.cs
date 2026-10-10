using HalfQR.Identity.Security;

namespace HalfQR.Identity.Endpoints;

// Resolves "Authorization: Bearer hqr_sess_..." into the signed-in user for dashboard endpoints.
public sealed class SessionAuthenticationFilter(SessionService sessions) : IEndpointFilter
{
    public const string ItemKey = "halfqr.session";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        var header = httpContext.Request.Headers.Authorization.ToString();
        var token = header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? header["Bearer ".Length..].Trim() : null;
        var result = await sessions.AuthenticateAsync(token, httpContext.RequestAborted);

        if (result is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Sign in required.");
        }

        httpContext.Items[ItemKey] = new AuthenticatedSession(result.Value.Session, result.Value.User);
        return await next(context);
    }
}

public static class HttpContextSessionExtensions
{
    public static AuthenticatedSession GetSession(this HttpContext httpContext)
        => (AuthenticatedSession)httpContext.Items[SessionAuthenticationFilter.ItemKey]!;
}
