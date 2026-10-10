using System.Text.Json;
using HalfQR.Identity.Models;
using Microsoft.Extensions.Options;
using Npgsql;

namespace HalfQR.Identity.Storage;

// Each table stores the record as jsonb plus the columns used for lookups.
public sealed class PostgresIdentityStore(IOptions<IdentityOptions> options) : IIdentityStore, IAsyncDisposable
{
    private const string UsersTable = "users";
    private const string SessionsTable = "sessions";
    private const string CredentialsTable = "api_credentials";
    private const string EmailVerificationsTable = "email_verifications";

    private readonly string _schema = QuoteIdentifier(options.Value.PostgresSchema);
    private readonly NpgsqlDataSource _dataSource = CreateDataSource(options.Value);
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private volatile bool _initialized;

    public Task<UserAccount?> GetUserAsync(Guid userId, CancellationToken cancellationToken)
        => QuerySingleAsync<UserAccount>(UsersTable, "id = @value", userId, cancellationToken);

    public Task<UserAccount?> FindUserByProviderAsync(string provider, string providerSubject, CancellationToken cancellationToken)
        => QuerySingleAsync<UserAccount>(UsersTable, "lookup = @value", $"{provider}:{providerSubject}", cancellationToken);

    public Task<UserAccount?> FindUserByEmailAsync(string email, CancellationToken cancellationToken)
        => QuerySingleAsync<UserAccount>(UsersTable, "record->>'email' = @value ORDER BY (record->>'createdAt') ASC", email, cancellationToken);

    public Task SaveUserAsync(UserAccount user, CancellationToken cancellationToken)
        => UpsertAsync(UsersTable, user.Id, user.Id, $"{user.Provider}:{user.ProviderSubject}", user, cancellationToken);

    public Task<SessionRecord?> FindSessionByTokenHashAsync(string tokenHash, CancellationToken cancellationToken)
        => QuerySingleAsync<SessionRecord>(SessionsTable, "lookup = @value", tokenHash, cancellationToken);

    public Task<IReadOnlyList<SessionRecord>> ListSessionsAsync(Guid userId, CancellationToken cancellationToken)
        => QueryManyAsync<SessionRecord>(SessionsTable, userId, cancellationToken);

    public Task SaveSessionAsync(SessionRecord session, CancellationToken cancellationToken)
        => UpsertAsync(SessionsTable, session.Id, session.UserId, session.TokenHash, session, cancellationToken);

    public Task<ApiCredential?> FindCredentialByTokenHashAsync(string tokenHash, CancellationToken cancellationToken)
        => QuerySingleAsync<ApiCredential>(CredentialsTable, "lookup = @value", tokenHash, cancellationToken);

    public Task<IReadOnlyList<ApiCredential>> ListCredentialsAsync(Guid userId, CancellationToken cancellationToken)
        => QueryManyAsync<ApiCredential>(CredentialsTable, userId, cancellationToken);

    public Task SaveCredentialAsync(ApiCredential credential, CancellationToken cancellationToken)
        => UpsertAsync(CredentialsTable, credential.Id, credential.UserId, credential.TokenHash, credential, cancellationToken);

    public Task<EmailVerification?> FindEmailVerificationByTokenHashAsync(string tokenHash, CancellationToken cancellationToken)
        => QuerySingleAsync<EmailVerification>(EmailVerificationsTable, "lookup = @value", tokenHash, cancellationToken);

    public Task<EmailVerification?> FindLatestEmailVerificationAsync(string email, CancellationToken cancellationToken)
        => QuerySingleAsync<EmailVerification>(EmailVerificationsTable, "record->>'email' = @value ORDER BY updated_at DESC", email, cancellationToken);

    // Email verifications have no user yet, so user_id holds Guid.Empty.
    public Task SaveEmailVerificationAsync(EmailVerification verification, CancellationToken cancellationToken)
        => UpsertAsync(EmailVerificationsTable, verification.Id, Guid.Empty, verification.TokenHash, verification, cancellationToken);

    public ValueTask DisposeAsync()
        => _dataSource.DisposeAsync();

    private async Task UpsertAsync<T>(string table, Guid id, Guid userId, string lookup, T record, CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var command = _dataSource.CreateCommand($"""
            INSERT INTO {_schema}.{table} (id, user_id, lookup, record, updated_at)
            VALUES (@id, @userId, @lookup, @record::jsonb, NOW())
            ON CONFLICT (id)
            DO UPDATE SET lookup = EXCLUDED.lookup, record = EXCLUDED.record, updated_at = NOW();
            """);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("userId", userId);
        command.Parameters.AddWithValue("lookup", lookup);
        command.Parameters.AddWithValue("record", JsonSerializer.Serialize(record, IdentityJson.SerializerOptions));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<T?> QuerySingleAsync<T>(string table, string predicate, object value, CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var command = _dataSource.CreateCommand($"SELECT record::text FROM {_schema}.{table} WHERE {predicate} LIMIT 1");
        command.Parameters.AddWithValue("value", value);

        return await command.ExecuteScalarAsync(cancellationToken) is string json
            ? JsonSerializer.Deserialize<T>(json, IdentityJson.SerializerOptions)
            : default;
    }

    private async Task<IReadOnlyList<T>> QueryManyAsync<T>(string table, Guid userId, CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var command = _dataSource.CreateCommand($"SELECT record::text FROM {_schema}.{table} WHERE user_id = @userId");
        command.Parameters.AddWithValue("userId", userId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var records = new List<T>();

        while (await reader.ReadAsync(cancellationToken))
        {
            records.Add(JsonSerializer.Deserialize<T>(reader.GetString(0), IdentityJson.SerializerOptions)!);
        }

        return records;
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            return;
        }

        await _initializationLock.WaitAsync(cancellationToken);

        try
        {
            if (_initialized)
            {
                return;
            }

            var tables = string.Join(Environment.NewLine, new[] { UsersTable, SessionsTable, CredentialsTable, EmailVerificationsTable }.Select(table => $"""
                CREATE TABLE IF NOT EXISTS {_schema}.{table} (
                    id uuid PRIMARY KEY,
                    user_id uuid NOT NULL,
                    lookup text NOT NULL UNIQUE,
                    record jsonb NOT NULL,
                    updated_at timestamptz NOT NULL DEFAULT NOW()
                );
                CREATE INDEX IF NOT EXISTS ix_{table}_user_id ON {_schema}.{table} (user_id);
                """));

            await using var command = _dataSource.CreateCommand($"CREATE SCHEMA IF NOT EXISTS {_schema};{Environment.NewLine}{tables}");
            await command.ExecuteNonQueryAsync(cancellationToken);
            _initialized = true;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    private static NpgsqlDataSource CreateDataSource(IdentityOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.PostgresConnectionString))
        {
            throw new InvalidOperationException("Identity PostgreSql storage is enabled but Identity:PostgresConnectionString is empty.");
        }

        return NpgsqlDataSource.Create(options.PostgresConnectionString);
    }

    private static string QuoteIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            throw new InvalidOperationException("Identity PostgreSql storage requires a non-empty schema name.");
        }

        return $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}
