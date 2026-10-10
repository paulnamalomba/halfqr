using HalfQR.Billing.Models;

namespace HalfQR.Billing.Storage;

public interface IBillingStore
{
    Task<Subscription?> GetSubscriptionAsync(Guid userId, CancellationToken cancellationToken);

    Task SaveSubscriptionAsync(Subscription subscription, CancellationToken cancellationToken);

    Task<BillingPayment?> GetPaymentAsync(Guid paymentId, CancellationToken cancellationToken);

    Task<BillingPayment?> FindPaymentByTxRefAsync(string txRef, CancellationToken cancellationToken);

    Task<IReadOnlyList<BillingPayment>> ListPaymentsAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<BillingPayment>> ListPendingPaymentsAsync(CancellationToken cancellationToken);

    Task SavePaymentAsync(BillingPayment payment, CancellationToken cancellationToken);
}
