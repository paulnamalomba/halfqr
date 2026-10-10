namespace HalfQR.Billing.Payments;

// Webhook fallback: re-verifies pending charges and expires ones the payer never approved.
public sealed class PaymentTimeoutWorker(BillingService billing, ILogger<PaymentTimeoutWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await billing.ExpireOrVerifyPendingAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Pending payment sweep failed.");
            }
        }
    }
}
