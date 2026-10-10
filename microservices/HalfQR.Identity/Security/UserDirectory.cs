using HalfQR.Identity.Models;
using HalfQR.Identity.Storage;

namespace HalfQR.Identity.Security;

public sealed class UserDirectory(IIdentityStore store, TimeProvider timeProvider)
{
    // Sign-in and sign-up share this path: the first sign-in creates the account.
    public async Task<UserAccount> UpsertAsync(string provider, string subject, string email, string displayName, string? avatarUrl, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        // Both Google and email sign-in verify the address, so an existing account with the same email is reused.
        var existing = await store.FindUserByProviderAsync(provider, subject, cancellationToken)
            ?? await store.FindUserByEmailAsync(email, cancellationToken);
        var user = existing is null
            ? new UserAccount
            {
                Id = Guid.NewGuid(),
                Provider = provider,
                ProviderSubject = subject,
                Email = email,
                DisplayName = displayName,
                AvatarUrl = avatarUrl,
                CreatedAt = now,
                LastSignInAt = now,
            }
            : existing with
            {
                Email = email,
                DisplayName = existing.Provider == provider ? displayName : existing.DisplayName,
                AvatarUrl = avatarUrl ?? existing.AvatarUrl,
                LastSignInAt = now,
            };

        await store.SaveUserAsync(user, cancellationToken);
        return user;
    }
}
