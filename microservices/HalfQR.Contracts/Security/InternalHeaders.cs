namespace HalfQR.Contracts.Security;

// Headers shared between services. Values are secrets configured per deployment, never sent to browsers.
public static class InternalHeaders
{
    // Service-to-service key used by PublicApi when calling Identity's internal endpoints.
    public const string InternalKey = "X-HalfQR-Internal-Key";

    // Shared secret the webapp proxy sends so PublicApi trusts its X-Forwarded-For for rate limiting.
    public const string ProxySecret = "X-HalfQR-Proxy-Secret";
}
