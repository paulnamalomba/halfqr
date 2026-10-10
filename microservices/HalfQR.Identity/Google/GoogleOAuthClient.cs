using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace HalfQR.Identity.Google;

public sealed record GoogleIdentity(string Subject, string Email, string DisplayName, string? AvatarUrl);

// Authorization-code exchange with PKCE. The id_token comes straight from Google's token endpoint over TLS,
// which OpenID Connect Core 3.1.3.7 allows to be trusted without a JWKS signature check; issuer, audience and expiry are still verified.
public sealed class GoogleOAuthClient(HttpClient httpClient, IOptions<GoogleOAuthOptions> options, TimeProvider timeProvider)
{
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    private static readonly string[] ValidIssuers = ["accounts.google.com", "https://accounts.google.com"];

    private readonly GoogleOAuthOptions _options = options.Value;

    public bool IsConfigured => _options.IsConfigured;

    public bool IsAllowedRedirectUri(string redirectUri)
        => _options.AllowedRedirectUris.Contains(redirectUri, StringComparer.Ordinal);

    public async Task<GoogleIdentity> ExchangeCodeAsync(string code, string redirectUri, string codeVerifier, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsync(TokenEndpoint, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["redirect_uri"] = redirectUri,
            ["code_verifier"] = codeVerifier,
            ["grant_type"] = "authorization_code",
        }), cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new GoogleOAuthException("Google rejected the authorization code.");
        }

        var tokens = await response.Content.ReadFromJsonAsync<GoogleTokenResponse>(cancellationToken)
            ?? throw new GoogleOAuthException("Google returned an empty token response.");

        return ReadIdentity(tokens.IdToken ?? throw new GoogleOAuthException("Google did not return an id_token."));
    }

    private GoogleIdentity ReadIdentity(string idToken)
    {
        var segments = idToken.Split('.');

        if (segments.Length != 3)
        {
            throw new GoogleOAuthException("Google returned a malformed id_token.");
        }

        var claims = JsonSerializer.Deserialize<GoogleIdTokenClaims>(Base64UrlDecode(segments[1]))
            ?? throw new GoogleOAuthException("Google returned an unreadable id_token.");

        if (!ValidIssuers.Contains(claims.Issuer, StringComparer.Ordinal)
            || !string.Equals(claims.Audience, _options.ClientId, StringComparison.Ordinal)
            || DateTimeOffset.FromUnixTimeSeconds(claims.ExpiresAt) <= timeProvider.GetUtcNow())
        {
            throw new GoogleOAuthException("Google id_token failed issuer, audience or expiry checks.");
        }

        if (string.IsNullOrWhiteSpace(claims.Subject) || string.IsNullOrWhiteSpace(claims.Email) || claims.EmailVerified != true)
        {
            throw new GoogleOAuthException("Google account must have a verified email address.");
        }

        return new GoogleIdentity(claims.Subject, claims.Email, claims.Name ?? claims.Email, claims.Picture);
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded += (padded.Length % 4) switch { 2 => "==", 3 => "=", _ => string.Empty };
        return Convert.FromBase64String(padded);
    }

    private sealed record GoogleTokenResponse([property: JsonPropertyName("id_token")] string? IdToken);

    private sealed record GoogleIdTokenClaims(
        [property: JsonPropertyName("iss")] string Issuer,
        [property: JsonPropertyName("aud")] string Audience,
        [property: JsonPropertyName("sub")] string Subject,
        [property: JsonPropertyName("exp")] long ExpiresAt,
        [property: JsonPropertyName("email")] string? Email,
        [property: JsonPropertyName("email_verified")] bool? EmailVerified,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("picture")] string? Picture);
}

public sealed class GoogleOAuthException(string message) : Exception(message);
