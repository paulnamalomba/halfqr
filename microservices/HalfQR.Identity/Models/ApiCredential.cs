using HalfQR.Contracts.Security;

namespace HalfQR.Identity.Models;

// API key or access token. The secret is shown once at creation; only its SHA-256 hash and display prefix are stored.
public sealed record ApiCredential
{
    public Guid Id { get; init; }

    public Guid UserId { get; init; }

    public ApiCredentialKind Kind { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Prefix { get; init; } = string.Empty;

    public string LastFour { get; init; } = string.Empty;

    public string TokenHash { get; init; } = string.Empty;

    public IReadOnlyList<string> Scopes { get; init; } = [];

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }

    public DateTimeOffset? LastUsedAt { get; init; }

    public DateTimeOffset? RevokedAt { get; init; }

    public bool IsActive(DateTimeOffset now)
        => RevokedAt is null && (ExpiresAt is null || ExpiresAt > now);
}
