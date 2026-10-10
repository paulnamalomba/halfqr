using HalfQR.Identity.Email;
using HalfQR.Identity.Models;
using HalfQR.Identity.Storage;
using Microsoft.Extensions.Options;

namespace HalfQR.Identity.Security;

public enum EmailLinkResult
{
    Sent,
    Throttled,
    Unavailable,
}

public sealed class EmailVerificationService(
    IIdentityStore store,
    IEmailSender emailSender,
    IOptions<IdentityOptions> options,
    TimeProvider timeProvider,
    ILogger<EmailVerificationService> logger)
{
    public const string TokenPrefix = "hqr_ev_";
    private const int ValidMinutes = 15;
    private static readonly TimeSpan ResendInterval = TimeSpan.FromSeconds(60);

    public async Task<EmailLinkResult> SendSignInLinkAsync(string email, CancellationToken cancellationToken)
    {
        if (!emailSender.IsConfigured)
        {
            return EmailLinkResult.Unavailable;
        }

        var now = timeProvider.GetUtcNow();
        var latest = await store.FindLatestEmailVerificationAsync(email, cancellationToken);

        if (latest is not null && now - latest.CreatedAt < ResendInterval)
        {
            return EmailLinkResult.Throttled;
        }

        var token = SecretTokens.Create(TokenPrefix);
        var verification = new EmailVerification
        {
            Id = Guid.NewGuid(),
            Email = email,
            TokenHash = SecretTokens.Hash(token),
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(ValidMinutes),
        };

        await store.SaveEmailVerificationAsync(verification, cancellationToken);

        var link = $"{options.Value.WebappBaseUrl.TrimEnd('/')}/api/auth/email/verify?token={Uri.EscapeDataString(token)}";

        try
        {
            await emailSender.SendAsync(VerificationEmailTemplate.Build(email, link, ValidMinutes), cancellationToken);
            return EmailLinkResult.Sent;
        }
        catch (Exception exception) when (exception is InvalidOperationException or HttpRequestException or TaskCanceledException)
        {
            logger.LogError(exception, "Sending the sign-in link failed.");
            return EmailLinkResult.Unavailable;
        }
    }

    // Returns the verified email, or null when the link is unknown, used or expired.
    public async Task<string?> ConsumeAsync(string? token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token) || !token.StartsWith(TokenPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();
        var verification = await store.FindEmailVerificationByTokenHashAsync(SecretTokens.Hash(token), cancellationToken);

        if (verification is null || !verification.IsUsable(now))
        {
            return null;
        }

        await store.SaveEmailVerificationAsync(verification with { ConsumedAt = now }, cancellationToken);
        return verification.Email;
    }
}
