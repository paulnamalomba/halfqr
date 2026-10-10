namespace HalfQR.Contracts.Security;

// Scopes granted to API keys and access tokens. PublicApi enforces them per route.
public static class ApiScopes
{
    public const string QrRender = "qr:render";

    public const string QrRead = "qr:read";

    public static readonly IReadOnlyList<string> All = [QrRender, QrRead];

    public static bool IsKnown(string scope)
        => All.Contains(scope, StringComparer.Ordinal);
}
