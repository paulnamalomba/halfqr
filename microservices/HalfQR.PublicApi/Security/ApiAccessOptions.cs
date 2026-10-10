namespace HalfQR.PublicApi.Security;

public sealed class ApiAccessOptions
{
    // When false, anonymous callers may render at the anonymous rate limit. Keys still get higher limits and job ownership.
    public bool RequireApiKey { get; set; }

    public string IdentityBaseUrl { get; set; } = "http://localhost:5240";

    // Must match Identity:InternalApiKey. Empty means API keys cannot be verified and keyed requests are rejected.
    public string InternalApiKey { get; set; } = string.Empty;

    // Shared with the webapp proxy so its X-Forwarded-For is trusted. Empty means forwarded addresses are ignored.
    public string ProxySecret { get; set; } = string.Empty;

    public int AnonymousRequestsPerMinute { get; set; } = 30;

    public int AnonymousDraftRequestsPerMinute { get; set; } = 90;

    public int KeyedRequestsPerMinute { get; set; } = 300;

    public int VerificationCacheSeconds { get; set; } = 60;
}
