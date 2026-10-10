using HalfQR.Identity.Models;
using HalfQR.Identity.Storage;
using Microsoft.Extensions.Options;

namespace HalfQR.Identity.Security;

public sealed class SessionService(IIdentityStore store, IOptions<IdentityOptions> options, TimeProvider timeProvider)
{
    private static readonly TimeSpan LastSeenWriteInterval = TimeSpan.FromMinutes(5);

    public async Task<(string Token, SessionRecord Session)> CreateAsync(Guid userId, string? userAgent, string? ipAddress, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var token = SecretTokens.Create(SecretTokens.SessionPrefix);
        var session = new SessionRecord
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = SecretTokens.Hash(token),
            CreatedAt = now,
            ExpiresAt = now.AddDays(options.Value.SessionLifetimeDays),
            LastSeenAt = now,
            UserAgent = Truncate(userAgent, 256),
            IpAddress = Truncate(ipAddress, 64),
        };

        await store.SaveSessionAsync(session, cancellationToken);
        return (token, session);
    }

    public async Task<(SessionRecord Session, UserAccount User)?> AuthenticateAsync(string? token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token) || !token.StartsWith(SecretTokens.SessionPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();
        var session = await store.FindSessionByTokenHashAsync(SecretTokens.Hash(token), cancellationToken);

        if (session is null || !session.IsActive(now))
        {
            return null;
        }

        var user = await store.GetUserAsync(session.UserId, cancellationToken);

        if (user is null)
        {
            return null;
        }

        if (now - session.LastSeenAt > LastSeenWriteInterval)
        {
            session = session with { LastSeenAt = now };
            await store.SaveSessionAsync(session, cancellationToken);
        }

        return (session, user);
    }

    public async Task<bool> RevokeAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken)
    {
        var session = (await store.ListSessionsAsync(userId, cancellationToken)).FirstOrDefault(existing => existing.Id == sessionId);

        if (session is null)
        {
            return false;
        }

        if (session.RevokedAt is null)
        {
            await store.SaveSessionAsync(session with { RevokedAt = timeProvider.GetUtcNow() }, cancellationToken);
        }

        return true;
    }

    private static string? Truncate(string? value, int maxLength)
        => value is { Length: > 0 } && value.Length > maxLength ? value[..maxLength] : value;
}
