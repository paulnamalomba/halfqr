namespace HalfQR.Identity.Models;

public sealed record UserAccount
{
    public Guid Id { get; init; }

    public string Email { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string? AvatarUrl { get; init; }

    // "google" or "dev". ProviderSubject is the provider's stable user id.
    public string Provider { get; init; } = string.Empty;

    public string ProviderSubject { get; init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset LastSignInAt { get; init; }
}
