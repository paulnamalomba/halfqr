using HalfQR.Contracts.Security;
using HalfQR.Identity.Models;
using HalfQR.Identity.Storage;
using Microsoft.Extensions.Options;

namespace HalfQR.Identity.Security;

public enum CredentialIssueError
{
    None,
    PaymentRequired,
    LimitReached,
    BillingUnavailable,
}

public sealed class CredentialService(IIdentityStore store, BillingClient billing, IOptions<IdentityOptions> options, TimeProvider timeProvider)
{
    public const int MaxNameLength = 64;
    public static readonly int[] AccessTokenLifetimesDays = [1, 7, 30, 90];
    public static readonly int[] ApiKeyLifetimesDays = [30, 90, 180, 365];

    private static readonly TimeSpan LastUsedWriteInterval = TimeSpan.FromMinutes(1);

    public static Dictionary<string, string[]> Validate(ApiCredentialKind kind, string? name, IReadOnlyList<string>? scopes, int? expiresInDays)
    {
        var errors = new Dictionary<string, string[]>();

        if (!Enum.IsDefined(kind))
        {
            errors["kind"] = ["Kind must be ApiKey or AccessToken."];
        }

        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > MaxNameLength)
        {
            errors["name"] = [$"Name is required and must be {MaxNameLength} characters or fewer."];
        }

        if (scopes is null || scopes.Count == 0 || scopes.Any(scope => !ApiScopes.IsKnown(scope)))
        {
            errors["scopes"] = [$"Choose at least one scope from: {string.Join(", ", ApiScopes.All)}."];
        }

        if (kind == ApiCredentialKind.AccessToken && (expiresInDays is null || !AccessTokenLifetimesDays.Contains(expiresInDays.Value)))
        {
            errors["expiresInDays"] = [$"Access tokens must expire in {string.Join(", ", AccessTokenLifetimesDays)} days."];
        }

        if (kind == ApiCredentialKind.ApiKey && expiresInDays is not null && !ApiKeyLifetimesDays.Contains(expiresInDays.Value))
        {
            errors["expiresInDays"] = [$"API keys can expire in {string.Join(", ", ApiKeyLifetimesDays)} days, or never."];
        }

        return errors;
    }

    public async Task<IReadOnlyList<ApiCredential>> ListAsync(Guid userId, CancellationToken cancellationToken)
        => (await store.ListCredentialsAsync(userId, cancellationToken))
            .OrderByDescending(static credential => credential.CreatedAt)
            .ToArray();

    // API keys and access tokens are paid features. The plan sets how many of each may be active at once.
    public async Task<CredentialIssueError> CanIssueAsync(Guid userId, ApiCredentialKind kind, bool replacingActive, CancellationToken cancellationToken)
    {
        var entitlement = await billing.GetEntitlementAsync(userId, cancellationToken, bypassCache: true);

        if (entitlement is null)
        {
            return CredentialIssueError.BillingUnavailable;
        }

        if (!entitlement.IsPaid)
        {
            return CredentialIssueError.PaymentRequired;
        }

        var now = timeProvider.GetUtcNow();
        var activeCount = (await store.ListCredentialsAsync(userId, cancellationToken))
            .Count(credential => credential.Kind == kind && credential.IsActive(now));
        var planLimit = kind == ApiCredentialKind.ApiKey ? entitlement.MaxApiKeys : entitlement.MaxAccessTokens;
        var limit = Math.Min(planLimit, options.Value.MaxActiveCredentialsPerKind);

        return activeCount - (replacingActive ? 1 : 0) >= limit
            ? CredentialIssueError.LimitReached
            : CredentialIssueError.None;
    }

    public async Task<(CredentialIssueError Error, string? Secret, ApiCredential? Credential)> CreateAsync(
        Guid userId,
        ApiCredentialKind kind,
        string name,
        IReadOnlyList<string> scopes,
        int? expiresInDays,
        CancellationToken cancellationToken)
    {
        var error = await CanIssueAsync(userId, kind, replacingActive: false, cancellationToken);

        return error == CredentialIssueError.None
            ? await IssueAsync(userId, kind, name, scopes, expiresInDays, cancellationToken)
            : (error, null, null);
    }

    private async Task<(CredentialIssueError Error, string? Secret, ApiCredential? Credential)> IssueAsync(
        Guid userId,
        ApiCredentialKind kind,
        string name,
        IReadOnlyList<string> scopes,
        int? expiresInDays,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        var secret = SecretTokens.Create(kind == ApiCredentialKind.ApiKey ? SecretTokens.ApiKeyPrefix : SecretTokens.AccessTokenPrefix);
        var credential = new ApiCredential
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Kind = kind,
            Name = name.Trim(),
            Prefix = SecretTokens.DisplayPrefix(secret),
            LastFour = secret[^4..],
            TokenHash = SecretTokens.Hash(secret),
            Scopes = scopes.Distinct(StringComparer.Ordinal).ToArray(),
            CreatedAt = now,
            ExpiresAt = expiresInDays is null ? null : now.AddDays(expiresInDays.Value),
        };

        await store.SaveCredentialAsync(credential, cancellationToken);
        return (CredentialIssueError.None, secret, credential);
    }

    public async Task<ApiCredential?> RevokeAsync(Guid userId, Guid credentialId, CancellationToken cancellationToken)
    {
        var credential = (await store.ListCredentialsAsync(userId, cancellationToken)).FirstOrDefault(existing => existing.Id == credentialId);

        if (credential is null || credential.RevokedAt is not null)
        {
            return credential;
        }

        credential = credential with { RevokedAt = timeProvider.GetUtcNow() };
        await store.SaveCredentialAsync(credential, cancellationToken);
        return credential;
    }

    // Revokes the old secret and issues a new one with the same name, scopes and lifetime.
    // Returns null when the credential does not exist or is no longer active.
    public async Task<(CredentialIssueError Error, string? Secret, ApiCredential? Credential)?> RotateAsync(Guid userId, Guid credentialId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var existing = (await store.ListCredentialsAsync(userId, cancellationToken)).FirstOrDefault(credential => credential.Id == credentialId);

        if (existing is null || !existing.IsActive(now))
        {
            return null;
        }

        var error = await CanIssueAsync(userId, existing.Kind, replacingActive: true, cancellationToken);

        if (error != CredentialIssueError.None)
        {
            return (error, null, null);
        }

        await store.SaveCredentialAsync(existing with { RevokedAt = now }, cancellationToken);

        int? lifetimeDays = existing.ExpiresAt is null
            ? null
            : (int)Math.Round((existing.ExpiresAt.Value - existing.CreatedAt).TotalDays);

        return await IssueAsync(userId, existing.Kind, existing.Name, existing.Scopes, lifetimeDays, cancellationToken);
    }

    public async Task<CredentialVerificationResponse> VerifyAsync(string? token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token)
            || !(token.StartsWith(SecretTokens.ApiKeyPrefix, StringComparison.Ordinal) || token.StartsWith(SecretTokens.AccessTokenPrefix, StringComparison.Ordinal)))
        {
            return CredentialVerificationResponse.Invalid;
        }

        var now = timeProvider.GetUtcNow();
        var credential = await store.FindCredentialByTokenHashAsync(SecretTokens.Hash(token), cancellationToken);

        if (credential is null || !credential.IsActive(now))
        {
            return CredentialVerificationResponse.Invalid;
        }

        // Credentials stop working when the owner's paid plan lapses, and start again on renewal.
        var entitlement = await billing.GetEntitlementAsync(credential.UserId, cancellationToken);

        if (entitlement is null || !entitlement.IsPaid)
        {
            return CredentialVerificationResponse.Invalid;
        }

        if (credential.LastUsedAt is null || now - credential.LastUsedAt > LastUsedWriteInterval)
        {
            await store.SaveCredentialAsync(credential with { LastUsedAt = now }, cancellationToken);
        }

        return new CredentialVerificationResponse
        {
            Valid = true,
            CredentialId = credential.Id,
            UserId = credential.UserId,
            Kind = credential.Kind,
            Scopes = credential.Scopes,
            ExpiresAt = credential.ExpiresAt,
            RequestsPerMinute = entitlement.RequestsPerMinute,
        };
    }
}
