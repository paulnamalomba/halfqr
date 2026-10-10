using System.Text.Json;
using HalfQR.Billing.Models;
using Microsoft.Extensions.Options;
using Npgsql;

namespace HalfQR.Billing.Storage;

public sealed class PostgresBillingStore(IOptions<BillingOptions> options) : IBillingStore, IAsyncDisposable
{
    private readonly string _schema = QuoteIdentifier(options.Value.PostgresSchema);
    private readonly NpgsqlDataSource _dataSource = CreateDataSource(options.Value);
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private volatile bool _initialized;

    public async Task<Subscription?> GetSubscriptionAsync(Guid userId, CancellationToken cancellationToken)
        => (await QueryAsync<Subscription>("subscriptions", "user_id = @value", userId, cancellationToken)).FirstOrDefault();

    public async Task SaveSubscriptionAsync(Subscription subscription, CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var command = _dataSource.CreateCommand($"""
            INSERT INTO {_schema}.subscriptions (user_id, record, updated_at)
            VALUES (@userId, @record::jsonb, NOW())
            ON CONFLICT (user_id) DO UPDATE SET record = EXCLUDED.record, updated_at = NOW();
            """);
        command.Parameters.AddWithValue("userId", subscription.UserId);
        command.Parameters.AddWithValue("record", JsonSerializer.Serialize(subscription, BillingJson.SerializerOptions));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<BillingPayment?> GetPaymentAsync(Guid paymentId, CancellationToken cancellationToken)
        => (await QueryAsync<BillingPayment>("payments", "id = @value", paymentId, cancellationToken)).FirstOrDefault();

    public async Task<BillingPayment?> FindPaymentByTxRefAsync(string txRef, CancellationToken cancellationToken)
        => (await QueryAsync<BillingPayment>("payments", "tx_ref = @value", txRef, cancellationToken)).FirstOrDefault();

    public Task<IReadOnlyList<BillingPayment>> ListPaymentsAsync(Guid userId, CancellationToken cancellationToken)
        => QueryAsync<BillingPayment>("payments", "user_id = @value", userId, cancellationToken);

    public Task<IReadOnlyList<BillingPayment>> ListPendingPaymentsAsync(CancellationToken cancellationToken)
        => QueryAsync<BillingPayment>("payments", "status = @value", nameof(BillingPaymentStatus.Pending), cancellationToken);

    public async Task SavePaymentAsync(BillingPayment payment, CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var command = _dataSource.CreateCommand($"""
            INSERT INTO {_schema}.payments (id, user_id, tx_ref, status, record, updated_at)
            VALUES (@id, @userId, @txRef, @status, @record::jsonb, NOW())
            ON CONFLICT (id) DO UPDATE SET status = EXCLUDED.status, record = EXCLUDED.record, updated_at = NOW();
            """);
        command.Parameters.AddWithValue("id", payment.Id);
        command.Parameters.AddWithValue("userId", payment.UserId);
        command.Parameters.AddWithValue("txRef", payment.TxRef);
        command.Parameters.AddWithValue("status", payment.Status.ToString());
        command.Parameters.AddWithValue("record", JsonSerializer.Serialize(payment, BillingJson.SerializerOptions));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public ValueTask DisposeAsync()
        => _dataSource.DisposeAsync();

    private async Task<IReadOnlyList<T>> QueryAsync<T>(string table, string predicate, object value, CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var command = _dataSource.CreateCommand($"SELECT record::text FROM {_schema}.{table} WHERE {predicate}");
        command.Parameters.AddWithValue("value", value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var records = new List<T>();

        while (await reader.ReadAsync(cancellationToken))
        {
            records.Add(JsonSerializer.Deserialize<T>(reader.GetString(0), BillingJson.SerializerOptions)!);
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

            await using var command = _dataSource.CreateCommand($"""
                CREATE SCHEMA IF NOT EXISTS {_schema};

                CREATE TABLE IF NOT EXISTS {_schema}.subscriptions (
                    user_id uuid PRIMARY KEY,
                    record jsonb NOT NULL,
                    updated_at timestamptz NOT NULL DEFAULT NOW()
                );

                CREATE TABLE IF NOT EXISTS {_schema}.payments (
                    id uuid PRIMARY KEY,
                    user_id uuid NOT NULL,
                    tx_ref text NOT NULL UNIQUE,
                    status text NOT NULL,
                    record jsonb NOT NULL,
                    updated_at timestamptz NOT NULL DEFAULT NOW()
                );

                CREATE INDEX IF NOT EXISTS ix_payments_user_id ON {_schema}.payments (user_id);
                CREATE INDEX IF NOT EXISTS ix_payments_status ON {_schema}.payments (status);
                """);
            await command.ExecuteNonQueryAsync(cancellationToken);
            _initialized = true;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    private static NpgsqlDataSource CreateDataSource(BillingOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.PostgresConnectionString))
        {
            throw new InvalidOperationException("Billing PostgreSql storage is enabled but Billing:PostgresConnectionString is empty.");
        }

        return NpgsqlDataSource.Create(options.PostgresConnectionString);
    }

    private static string QuoteIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            throw new InvalidOperationException("Billing PostgreSql storage requires a non-empty schema name.");
        }

        return $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}
