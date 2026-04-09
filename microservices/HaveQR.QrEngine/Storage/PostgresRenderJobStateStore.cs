using System.Text.Json;
using HaveQR.Contracts.Models;
using HaveQR.Contracts.Options;
using Microsoft.Extensions.Options;
using Npgsql;

namespace HaveQR.QrEngine.Storage;

public sealed class PostgresRenderJobStateStore(IOptions<PostgresRenderStoreOptions> options) : IRenderJobStateStore, IAsyncDisposable
{
    private readonly PostgresRenderStoreOptions _options = options.Value;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private readonly NpgsqlDataSource _dataSource = CreateDataSource(options.Value);
    private volatile bool _initialized;

    public async Task SaveAsync(RenderJobState state, CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken);
        var json = JsonSerializer.Serialize(state, RenderJobStateJson.SerializerOptions);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            INSERT INTO {GetQualifiedTableName()}
            (job_id, state_json, updated_at)
            VALUES (@jobId, @stateJson::jsonb, NOW())
            ON CONFLICT (job_id)
            DO UPDATE SET state_json = EXCLUDED.state_json, updated_at = NOW();
            """;
        command.Parameters.AddWithValue("jobId", state.JobId);
        command.Parameters.AddWithValue("stateJson", json);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<RenderJobState?> GetAsync(Guid jobId, CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT state_json::text FROM {GetQualifiedTableName()} WHERE job_id = @jobId";
        command.Parameters.AddWithValue("jobId", jobId);

        var result = await command.ExecuteScalarAsync(cancellationToken);

        return result is string json && !string.IsNullOrWhiteSpace(json)
            ? JsonSerializer.Deserialize<RenderJobState>(json, RenderJobStateJson.SerializerOptions)
            : null;
    }

    public ValueTask DisposeAsync()
        => _dataSource.DisposeAsync();

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

            await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = $"""
                CREATE SCHEMA IF NOT EXISTS {QuoteIdentifier(_options.Schema)};

                CREATE TABLE IF NOT EXISTS {GetQualifiedTableName()} (
                    job_id uuid PRIMARY KEY,
                    state_json jsonb NOT NULL,
                    updated_at timestamptz NOT NULL DEFAULT NOW()
                );
                """;
            await command.ExecuteNonQueryAsync(cancellationToken);
            _initialized = true;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    private string GetQualifiedTableName()
        => $"{QuoteIdentifier(_options.Schema)}.{QuoteIdentifier(_options.TableName)}";

    private static NpgsqlDataSource CreateDataSource(PostgresRenderStoreOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            throw new InvalidOperationException("PostgreSQL storage is enabled but PostgresRenderStore:ConnectionString is empty.");
        }

        return NpgsqlDataSource.Create(options.ConnectionString);
    }

    private static string QuoteIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            throw new InvalidOperationException("PostgreSQL storage requires non-empty schema and table names.");
        }

        return $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}