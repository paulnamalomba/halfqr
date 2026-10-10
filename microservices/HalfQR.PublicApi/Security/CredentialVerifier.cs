using System.Security.Cryptography;
using System.Text;
using HalfQR.Contracts.Security;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace HalfQR.PublicApi.Security;

// Calls Identity to verify API keys and access tokens. Results are cached by token hash so revocation takes effect within the cache window.
public sealed class CredentialVerifier(HttpClient httpClient, IMemoryCache cache, IOptions<ApiAccessOptions> options, ILogger<CredentialVerifier> logger)
{
    private readonly ApiAccessOptions _options = options.Value;

    public async Task<CredentialVerificationResponse> VerifyAsync(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_options.InternalApiKey))
        {
            return CredentialVerificationResponse.Invalid;
        }

        var cacheKey = "credential:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

        if (cache.TryGetValue(cacheKey, out CredentialVerificationResponse? cached) && cached is not null)
        {
            return cached;
        }

        CredentialVerificationResponse result;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/internal/v1/credentials/verify")
            {
                Content = JsonContent.Create(new CredentialVerificationRequest(token)),
            };
            request.Headers.Add(InternalHeaders.InternalKey, _options.InternalApiKey);

            using var response = await httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            result = await response.Content.ReadFromJsonAsync<CredentialVerificationResponse>(cancellationToken) ?? CredentialVerificationResponse.Invalid;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            // Fail closed and do not cache, so a brief Identity outage does not lock keys out for the cache window.
            logger.LogWarning(exception, "Credential verification against Identity failed.");
            return CredentialVerificationResponse.Invalid;
        }

        var lifetime = TimeSpan.FromSeconds(result.Valid ? _options.VerificationCacheSeconds : Math.Min(_options.VerificationCacheSeconds, 15));

        if (result.ExpiresAt is { } expiresAt && expiresAt - DateTimeOffset.UtcNow < lifetime)
        {
            lifetime = expiresAt - DateTimeOffset.UtcNow;
        }

        if (lifetime > TimeSpan.Zero)
        {
            cache.Set(cacheKey, result, lifetime);
        }

        return result;
    }
}
