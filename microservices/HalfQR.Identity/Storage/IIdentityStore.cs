using HalfQR.Identity.Models;

namespace HalfQR.Identity.Storage;

public interface IIdentityStore
{
    Task<UserAccount?> GetUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<UserAccount?> FindUserByProviderAsync(string provider, string providerSubject, CancellationToken cancellationToken);

    Task<UserAccount?> FindUserByEmailAsync(string email, CancellationToken cancellationToken);

    Task SaveUserAsync(UserAccount user, CancellationToken cancellationToken);

    Task<SessionRecord?> FindSessionByTokenHashAsync(string tokenHash, CancellationToken cancellationToken);

    Task<IReadOnlyList<SessionRecord>> ListSessionsAsync(Guid userId, CancellationToken cancellationToken);

    Task SaveSessionAsync(SessionRecord session, CancellationToken cancellationToken);

    Task<ApiCredential?> FindCredentialByTokenHashAsync(string tokenHash, CancellationToken cancellationToken);

    Task<IReadOnlyList<ApiCredential>> ListCredentialsAsync(Guid userId, CancellationToken cancellationToken);

    Task SaveCredentialAsync(ApiCredential credential, CancellationToken cancellationToken);

    Task<EmailVerification?> FindEmailVerificationByTokenHashAsync(string tokenHash, CancellationToken cancellationToken);

    Task<EmailVerification?> FindLatestEmailVerificationAsync(string email, CancellationToken cancellationToken);

    Task SaveEmailVerificationAsync(EmailVerification verification, CancellationToken cancellationToken);
}
