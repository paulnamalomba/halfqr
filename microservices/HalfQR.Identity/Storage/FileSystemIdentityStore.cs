using System.Text.Json;
using HalfQR.Identity.Models;
using Microsoft.Extensions.Options;

namespace HalfQR.Identity.Storage;

// Single-file store for local development. Not for multi-instance deployments; use the PostgreSql provider there.
public sealed class FileSystemIdentityStore(IOptions<IdentityOptions> options) : IIdentityStore
{
    private readonly string _filePath = options.Value.FileSystemPath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private Snapshot? _snapshot;

    public Task<UserAccount?> GetUserAsync(Guid userId, CancellationToken cancellationToken)
        => ReadAsync(snapshot => snapshot.Users.FirstOrDefault(user => user.Id == userId), cancellationToken);

    public Task<UserAccount?> FindUserByProviderAsync(string provider, string providerSubject, CancellationToken cancellationToken)
        => ReadAsync(snapshot => snapshot.Users.FirstOrDefault(user => user.Provider == provider && user.ProviderSubject == providerSubject), cancellationToken);

    public Task<UserAccount?> FindUserByEmailAsync(string email, CancellationToken cancellationToken)
        => ReadAsync(snapshot => snapshot.Users.FirstOrDefault(user => user.Email == email), cancellationToken);

    public Task SaveUserAsync(UserAccount user, CancellationToken cancellationToken)
        => WriteAsync(snapshot => Upsert(snapshot.Users, user, existing => existing.Id == user.Id), cancellationToken);

    public Task<SessionRecord?> FindSessionByTokenHashAsync(string tokenHash, CancellationToken cancellationToken)
        => ReadAsync(snapshot => snapshot.Sessions.FirstOrDefault(session => session.TokenHash == tokenHash), cancellationToken);

    public Task<IReadOnlyList<SessionRecord>> ListSessionsAsync(Guid userId, CancellationToken cancellationToken)
        => ReadAsync<IReadOnlyList<SessionRecord>>(snapshot => snapshot.Sessions.Where(session => session.UserId == userId).ToArray(), cancellationToken);

    public Task SaveSessionAsync(SessionRecord session, CancellationToken cancellationToken)
        => WriteAsync(snapshot => Upsert(snapshot.Sessions, session, existing => existing.Id == session.Id), cancellationToken);

    public Task<ApiCredential?> FindCredentialByTokenHashAsync(string tokenHash, CancellationToken cancellationToken)
        => ReadAsync(snapshot => snapshot.Credentials.FirstOrDefault(credential => credential.TokenHash == tokenHash), cancellationToken);

    public Task<IReadOnlyList<ApiCredential>> ListCredentialsAsync(Guid userId, CancellationToken cancellationToken)
        => ReadAsync<IReadOnlyList<ApiCredential>>(snapshot => snapshot.Credentials.Where(credential => credential.UserId == userId).ToArray(), cancellationToken);

    public Task SaveCredentialAsync(ApiCredential credential, CancellationToken cancellationToken)
        => WriteAsync(snapshot => Upsert(snapshot.Credentials, credential, existing => existing.Id == credential.Id), cancellationToken);

    public Task<EmailVerification?> FindEmailVerificationByTokenHashAsync(string tokenHash, CancellationToken cancellationToken)
        => ReadAsync(snapshot => snapshot.EmailVerifications.FirstOrDefault(verification => verification.TokenHash == tokenHash), cancellationToken);

    public Task<EmailVerification?> FindLatestEmailVerificationAsync(string email, CancellationToken cancellationToken)
        => ReadAsync(snapshot => snapshot.EmailVerifications.Where(verification => verification.Email == email).MaxBy(static verification => verification.CreatedAt), cancellationToken);

    public Task SaveEmailVerificationAsync(EmailVerification verification, CancellationToken cancellationToken)
        => WriteAsync(snapshot =>
        {
            // Expired links are dropped on write so the file does not grow without bound.
            var cutoff = DateTimeOffset.UtcNow.AddDays(-1);
            snapshot.EmailVerifications.RemoveAll(existing => existing.ExpiresAt < cutoff);
            Upsert(snapshot.EmailVerifications, verification, existing => existing.Id == verification.Id);
        }, cancellationToken);

    private async Task<T> ReadAsync<T>(Func<Snapshot, T> read, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);

        try
        {
            return read(await LoadAsync(cancellationToken));
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task WriteAsync(Action<Snapshot> write, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);

        try
        {
            var snapshot = await LoadAsync(cancellationToken);
            write(snapshot);

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_filePath))!);
            var temporaryPath = _filePath + ".tmp";
            await File.WriteAllTextAsync(temporaryPath, JsonSerializer.Serialize(snapshot, IdentityJson.SerializerOptions), cancellationToken);
            File.Move(temporaryPath, _filePath, overwrite: true);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<Snapshot> LoadAsync(CancellationToken cancellationToken)
    {
        if (_snapshot is not null)
        {
            return _snapshot;
        }

        _snapshot = File.Exists(_filePath)
            ? JsonSerializer.Deserialize<Snapshot>(await File.ReadAllTextAsync(_filePath, cancellationToken), IdentityJson.SerializerOptions) ?? new Snapshot()
            : new Snapshot();

        return _snapshot;
    }

    private static void Upsert<T>(List<T> items, T item, Predicate<T> match)
    {
        var index = items.FindIndex(match);

        if (index >= 0)
        {
            items[index] = item;
        }
        else
        {
            items.Add(item);
        }
    }

    private sealed class Snapshot
    {
        public List<UserAccount> Users { get; init; } = [];

        public List<SessionRecord> Sessions { get; init; } = [];

        public List<ApiCredential> Credentials { get; init; } = [];

        public List<EmailVerification> EmailVerifications { get; init; } = [];
    }
}
