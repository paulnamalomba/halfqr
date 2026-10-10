namespace HalfQR.Contracts.Security;

public sealed record CredentialVerificationResponse
{
    public bool Valid { get; init; }

    public Guid? CredentialId { get; init; }

    public Guid? UserId { get; init; }

    public ApiCredentialKind? Kind { get; init; }

    public IReadOnlyList<string> Scopes { get; init; } = [];

    public DateTimeOffset? ExpiresAt { get; init; }

    // Per-minute limit from the owner's paid plan. PublicApi falls back to its keyed default when null.
    public int? RequestsPerMinute { get; init; }

    public static CredentialVerificationResponse Invalid { get; } = new();
}
