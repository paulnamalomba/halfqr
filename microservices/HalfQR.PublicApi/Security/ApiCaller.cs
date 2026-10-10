using HalfQR.Contracts.Security;

namespace HalfQR.PublicApi.Security;

// Resolved once per request by ApiCallerMiddleware.
public sealed record ApiCaller(Guid? CredentialId, IReadOnlyList<string> Scopes, string ClientAddress, int? RequestsPerMinute = null)
{
    public const string ItemKey = "halfqr.caller";

    public bool IsAnonymous => CredentialId is null;

    public string PartitionKey => CredentialId is { } id ? $"key:{id:N}" : $"ip:{ClientAddress}";

    public bool HasScope(string scope) => Scopes.Contains(scope, StringComparer.Ordinal);

    public static ApiCaller Anonymous(string clientAddress) => new(null, [], clientAddress);

    public static ApiCaller FromCredential(CredentialVerificationResponse verification, string clientAddress)
        => new(verification.CredentialId, verification.Scopes, clientAddress, verification.RequestsPerMinute);
}

public static class HttpContextCallerExtensions
{
    public static ApiCaller GetCaller(this HttpContext httpContext)
        => (ApiCaller)httpContext.Items[ApiCaller.ItemKey]!;
}
