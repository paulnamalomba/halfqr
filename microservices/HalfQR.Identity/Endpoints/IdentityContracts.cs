using HalfQR.Contracts.Security;
using HalfQR.Identity.Models;

namespace HalfQR.Identity.Endpoints;

public sealed record AuthConfigResponse(bool GoogleEnabled, string? GoogleClientId, bool EmailEnabled, bool DevSignInEnabled);

public sealed record EmailSignInRequest(string? Email);

public sealed record EmailVerifyRequest(string? Token);

public sealed record CheckoutRequest(string? PlanId, string? Operator, string? PhoneNumber);

public sealed record GoogleExchangeRequest(string Code, string RedirectUri, string CodeVerifier);

public sealed record DevSignInRequest(string Email, string? DisplayName);

public sealed record SessionIssuedResponse(string SessionToken, DateTimeOffset ExpiresAt, UserResponse User);

public sealed record UserResponse(Guid Id, string Email, string DisplayName, string? AvatarUrl, string Provider, DateTimeOffset CreatedAt, DateTimeOffset LastSignInAt)
{
    public static UserResponse From(UserAccount user)
        => new(user.Id, user.Email, user.DisplayName, user.AvatarUrl, user.Provider, user.CreatedAt, user.LastSignInAt);
}

public sealed record SessionResponse(
    Guid Id,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset LastSeenAt,
    string? UserAgent,
    string? IpAddress,
    DateTimeOffset? RevokedAt,
    bool Active,
    bool Current)
{
    public static SessionResponse From(SessionRecord session, Guid currentSessionId, DateTimeOffset now)
        => new(
            session.Id,
            session.CreatedAt,
            session.ExpiresAt,
            session.LastSeenAt,
            session.UserAgent,
            session.IpAddress,
            session.RevokedAt,
            session.IsActive(now),
            session.Id == currentSessionId);
}

public sealed record CredentialResponse(
    Guid Id,
    ApiCredentialKind Kind,
    string Name,
    string Prefix,
    string LastFour,
    IReadOnlyList<string> Scopes,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? LastUsedAt,
    DateTimeOffset? RevokedAt,
    string Status)
{
    public static CredentialResponse From(ApiCredential credential, DateTimeOffset now)
        => new(
            credential.Id,
            credential.Kind,
            credential.Name,
            credential.Prefix,
            credential.LastFour,
            credential.Scopes,
            credential.CreatedAt,
            credential.ExpiresAt,
            credential.LastUsedAt,
            credential.RevokedAt,
            credential.RevokedAt is not null ? "revoked" : credential.IsActive(now) ? "active" : "expired");
}

public sealed record CreateCredentialRequest(ApiCredentialKind Kind, string Name, IReadOnlyList<string>? Scopes, int? ExpiresInDays);

// Secret is returned exactly once, at creation or rotation.
public sealed record CredentialCreatedResponse(CredentialResponse Credential, string Secret);

public sealed record AuthenticatedSession(SessionRecord Session, UserAccount User);
