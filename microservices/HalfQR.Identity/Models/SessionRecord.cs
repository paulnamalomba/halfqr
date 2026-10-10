namespace HalfQR.Identity.Models;

// Browser session for the dashboard. Only the SHA-256 hash of the token is stored.
public sealed record SessionRecord
{
    public Guid Id { get; init; }

    public Guid UserId { get; init; }

    public string TokenHash { get; init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset ExpiresAt { get; init; }

    public DateTimeOffset LastSeenAt { get; init; }

    public string? UserAgent { get; init; }

    public string? IpAddress { get; init; }

    public DateTimeOffset? RevokedAt { get; init; }

    public bool IsActive(DateTimeOffset now)
        => RevokedAt is null && ExpiresAt > now;
}
