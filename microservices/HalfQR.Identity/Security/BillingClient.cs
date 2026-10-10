using System.Net;
using HalfQR.Contracts.Billing;
using HalfQR.Contracts.Security;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace HalfQR.Identity.Security;

// Calls HalfQR.Billing's internal API. Entitlement lookups fail closed: if Billing is unreachable, no paid features are granted.
public sealed class BillingClient(HttpClient httpClient, IMemoryCache cache, IOptions<IdentityOptions> options, ILogger<BillingClient> logger)
{
    private static readonly TimeSpan EntitlementCacheLifetime = TimeSpan.FromSeconds(60);

    public async Task<EntitlementResponse?> GetEntitlementAsync(Guid userId, CancellationToken cancellationToken, bool bypassCache = false)
    {
        var cacheKey = $"entitlement:{userId:N}";

        if (!bypassCache && cache.TryGetValue(cacheKey, out EntitlementResponse? cached) && cached is not null)
        {
            return cached;
        }

        try
        {
            using var response = await SendAsync(HttpMethod.Get, $"/internal/v1/users/{userId}/entitlement", null, cancellationToken);
            response.EnsureSuccessStatusCode();
            var entitlement = await response.Content.ReadFromJsonAsync<EntitlementResponse>(cancellationToken);

            if (entitlement is not null)
            {
                cache.Set(cacheKey, entitlement, EntitlementCacheLifetime);
            }

            return entitlement;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(exception, "Billing entitlement lookup failed for {UserId}.", userId);
            return null;
        }
    }

    public void InvalidateEntitlement(Guid userId)
        => cache.Remove($"entitlement:{userId:N}");

    // Relays a dashboard billing request to Billing and returns its status and JSON body unchanged.
    public async Task<IResult> ForwardAsync(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await SendAsync(method, path, body, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/json";
            return Results.Content(content, contentType, statusCode: (int)response.StatusCode);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(exception, "Billing request {Path} failed.", path);
            return Results.Problem(statusCode: (int)HttpStatusCode.ServiceUnavailable, title: "Billing is temporarily unavailable.");
        }
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, new Uri(new Uri(options.Value.BillingBaseUrl), path));
        request.Headers.Add(InternalHeaders.InternalKey, options.Value.InternalApiKey);

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await httpClient.SendAsync(request, cancellationToken);
    }
}
