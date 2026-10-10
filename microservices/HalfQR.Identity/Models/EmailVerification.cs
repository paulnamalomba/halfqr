namespace HalfQR.Identity.Models;

// One-time sign-in link sent by email. Only the token hash is stored.
public sealed record EmailVerification
{
    public Guid Id { get; init; }

    public string Email { get; init; } = string.Empty;

    public string TokenHash { get; init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset ExpiresAt { get; init; }

    public DateTimeOffset? ConsumedAt { get; init; }

    public bool IsUsable(DateTimeOffset now)
        => ConsumedAt is null && ExpiresAt > now;
}
