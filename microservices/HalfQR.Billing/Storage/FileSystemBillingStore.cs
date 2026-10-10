using System.Text.Json;
using HalfQR.Billing.Models;
using Microsoft.Extensions.Options;

namespace HalfQR.Billing.Storage;

// Single-file store for local development. Use the PostgreSql provider for deployments.
public sealed class FileSystemBillingStore(IOptions<BillingOptions> options) : IBillingStore
{
    private readonly string _filePath = options.Value.FileSystemPath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private Snapshot? _snapshot;

    public Task<Subscription?> GetSubscriptionAsync(Guid userId, CancellationToken cancellationToken)
        => ReadAsync(snapshot => snapshot.Subscriptions.FirstOrDefault(subscription => subscription.UserId == userId), cancellationToken);

    public Task SaveSubscriptionAsync(Subscription subscription, CancellationToken cancellationToken)
        => WriteAsync(snapshot => Upsert(snapshot.Subscriptions, subscription, existing => existing.UserId == subscription.UserId), cancellationToken);

    public Task<BillingPayment?> GetPaymentAsync(Guid paymentId, CancellationToken cancellationToken)
        => ReadAsync(snapshot => snapshot.Payments.FirstOrDefault(payment => payment.Id == paymentId), cancellationToken);

    public Task<BillingPayment?> FindPaymentByTxRefAsync(string txRef, CancellationToken cancellationToken)
        => ReadAsync(snapshot => snapshot.Payments.FirstOrDefault(payment => payment.TxRef == txRef), cancellationToken);

    public Task<IReadOnlyList<BillingPayment>> ListPaymentsAsync(Guid userId, CancellationToken cancellationToken)
        => ReadAsync<IReadOnlyList<BillingPayment>>(snapshot => snapshot.Payments.Where(payment => payment.UserId == userId).ToArray(), cancellationToken);

    public Task<IReadOnlyList<BillingPayment>> ListPendingPaymentsAsync(CancellationToken cancellationToken)
        => ReadAsync<IReadOnlyList<BillingPayment>>(snapshot => snapshot.Payments.Where(static payment => payment.Status == BillingPaymentStatus.Pending).ToArray(), cancellationToken);

    public Task SavePaymentAsync(BillingPayment payment, CancellationToken cancellationToken)
        => WriteAsync(snapshot => Upsert(snapshot.Payments, payment, existing => existing.Id == payment.Id), cancellationToken);

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
            await File.WriteAllTextAsync(temporaryPath, JsonSerializer.Serialize(snapshot, BillingJson.SerializerOptions), cancellationToken);
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
            ? JsonSerializer.Deserialize<Snapshot>(await File.ReadAllTextAsync(_filePath, cancellationToken), BillingJson.SerializerOptions) ?? new Snapshot()
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
        public List<Subscription> Subscriptions { get; init; } = [];

        public List<BillingPayment> Payments { get; init; } = [];
    }
}
