using HalfQR.Billing.Models;
using HalfQR.Billing.PayChangu;
using HalfQR.Billing.Storage;
using HalfQR.Contracts.Billing;
using Microsoft.Extensions.Options;

namespace HalfQR.Billing.Payments;

public sealed record CheckoutResult(BillingPayment? Payment, Dictionary<string, string[]>? Errors, string? ProblemTitle);

public sealed class BillingService(
    IBillingStore store,
    PayChanguClient payChangu,
    IOptions<BillingOptions> billingOptions,
    IOptions<PayChanguOptions> payChanguOptions,
    TimeProvider timeProvider,
    ILogger<BillingService> logger)
{
    private readonly BillingOptions _billing = billingOptions.Value;
    private readonly PayChanguOptions _payChangu = payChanguOptions.Value;

    public IReadOnlyList<BillingPlan> Plans => _billing.Plans;

    public IReadOnlyDictionary<string, MobileMoneyOperator> Operators => _payChangu.Operators;

    public bool IsMockMode => payChangu.IsMockMode;

    public async Task<EntitlementResponse> GetEntitlementAsync(Guid userId, CancellationToken cancellationToken)
    {
        var subscription = await store.GetSubscriptionAsync(userId, cancellationToken);
        var plan = subscription is not null && subscription.IsActive(timeProvider.GetUtcNow())
            ? FindPlan(subscription.PlanId)
            : null;

        return plan is null
            ? EntitlementResponse.Free
            : new EntitlementResponse
            {
                PlanId = plan.Id,
                PlanName = plan.Name,
                IsPaid = true,
                ActiveUntil = subscription!.CurrentPeriodEnd,
                MaxApiKeys = plan.MaxApiKeys,
                MaxAccessTokens = plan.MaxAccessTokens,
                RequestsPerMinute = plan.RequestsPerMinute,
            };
    }

    public Task<Subscription?> GetSubscriptionAsync(Guid userId, CancellationToken cancellationToken)
        => store.GetSubscriptionAsync(userId, cancellationToken);

    public async Task<IReadOnlyList<BillingPayment>> ListPaymentsAsync(Guid userId, CancellationToken cancellationToken)
        => (await store.ListPaymentsAsync(userId, cancellationToken))
            .OrderByDescending(static payment => payment.CreatedAt)
            .Take(20)
            .ToArray();

    public async Task<CheckoutResult> StartCheckoutAsync(Guid userId, string? planId, string? operatorKey, string? phoneNumber, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        var plan = FindPlan(planId);
        var mobile = PayChanguClient.NormalizeMalawiMobile(phoneNumber);
        MobileMoneyOperator? mobileOperator = null;

        if (plan is null || plan.AmountMwk <= 0)
        {
            errors["planId"] = ["Choose a paid plan."];
        }

        if (string.IsNullOrWhiteSpace(operatorKey) || !_payChangu.Operators.TryGetValue(operatorKey, out mobileOperator))
        {
            errors["operator"] = [$"Choose a mobile money operator: {string.Join(", ", _payChangu.Operators.Keys)}."];
        }

        if (mobile.Length != 9)
        {
            errors["phoneNumber"] = ["Enter a 9-digit Malawi mobile number, for example 991 234 567."];
        }
        else if (mobileOperator is not null && !string.IsNullOrEmpty(mobileOperator.NumberPrefix) && !mobile.StartsWith(mobileOperator.NumberPrefix, StringComparison.Ordinal))
        {
            errors["phoneNumber"] = [$"{mobileOperator.Label} numbers start with {mobileOperator.NumberPrefix}."];
        }

        if (errors.Count > 0)
        {
            return new CheckoutResult(null, errors, null);
        }

        if (!payChangu.IsConfigured)
        {
            return new CheckoutResult(null, null, "Payments are not configured yet. Please try again later.");
        }

        var pending = (await store.ListPaymentsAsync(userId, cancellationToken)).FirstOrDefault(static payment => payment.Status == BillingPaymentStatus.Pending);

        if (pending is not null)
        {
            return new CheckoutResult(null, null, "A payment is already waiting for approval on your phone. Approve or let it expire before starting another.");
        }

        var now = timeProvider.GetUtcNow();
        var paymentId = Guid.NewGuid();
        var payment = new BillingPayment
        {
            Id = paymentId,
            UserId = userId,
            PlanId = plan!.Id,
            Amount = plan.AmountMwk,
            Currency = _payChangu.Currency,
            Operator = operatorKey!,
            MobileLastFour = mobile[^4..],
            TxRef = $"HQR-{paymentId:N}-{now.ToUnixTimeSeconds()}",
            CreatedAt = now,
            StatusMessage = "Approve the payment prompt on your phone.",
        };

        await store.SavePaymentAsync(payment, cancellationToken);

        var initiation = await payChangu.InitiateMobileMoneyAsync(payment, mobileOperator!.OperatorId, mobile, cancellationToken);

        payment = initiation.Success
            ? payment with { ProviderChargeId = initiation.ChargeId, ProviderReferenceId = initiation.ReferenceId }
            : payment with { Status = BillingPaymentStatus.Failed, StatusMessage = initiation.ErrorMessage, CompletedAt = now };

        await store.SavePaymentAsync(payment, cancellationToken);
        return new CheckoutResult(payment, null, null);
    }

    // Re-queries PayChangu and applies the result. Safe to call repeatedly; terminal payments are not re-processed.
    public async Task<BillingPayment?> RefreshAsync(Guid paymentId, Guid? expectedUserId, CancellationToken cancellationToken)
    {
        var payment = await store.GetPaymentAsync(paymentId, cancellationToken);

        if (payment is null || (expectedUserId is not null && payment.UserId != expectedUserId))
        {
            return null;
        }

        return payment.IsTerminal ? payment : await VerifyAndApplyAsync(payment, cancellationToken);
    }

    public async Task<bool> ProcessWebhookAsync(string rawBody, CancellationToken cancellationToken)
    {
        var reference = PayChanguClient.ReadWebhookReference(rawBody);

        if (string.IsNullOrWhiteSpace(reference))
        {
            logger.LogWarning("PayChangu webhook had no tx_ref or charge_id.");
            return false;
        }

        var payment = await store.FindPaymentByTxRefAsync(reference, cancellationToken);

        if (payment is null)
        {
            logger.LogWarning("PayChangu webhook for unknown reference {Reference}.", reference);
            return false;
        }

        if (!payment.IsTerminal)
        {
            await VerifyAndApplyAsync(payment, cancellationToken);
        }

        return true;
    }

    public async Task ExpireOrVerifyPendingAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var timeout = TimeSpan.FromMinutes(_billing.PendingPaymentTimeoutMinutes);

        foreach (var payment in await store.ListPendingPaymentsAsync(cancellationToken))
        {
            var updated = await VerifyAndApplyAsync(payment, cancellationToken);

            if (updated.Status == BillingPaymentStatus.Pending && now - payment.CreatedAt > timeout)
            {
                await store.SavePaymentAsync(updated with
                {
                    Status = BillingPaymentStatus.Expired,
                    CompletedAt = now,
                    StatusMessage = "The payment prompt expired before it was approved.",
                }, cancellationToken);
            }
        }
    }

    private async Task<BillingPayment> VerifyAndApplyAsync(BillingPayment payment, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var verification = await payChangu.VerifyAsync(payment, cancellationToken);

        if (verification is null)
        {
            payment = payment with { LastVerifiedAt = now };
            await store.SavePaymentAsync(payment, cancellationToken);
            return payment;
        }

        payment = payment with
        {
            LastVerifiedAt = now,
            ProviderChargeId = payment.ProviderChargeId ?? verification.ChargeId,
        };

        payment = verification.Status switch
        {
            ProviderPaymentStatus.Success when verification.DetailsMatch => payment with
            {
                Status = BillingPaymentStatus.Completed,
                CompletedAt = now,
                StatusMessage = "Payment confirmed. Your plan is active.",
            },
            ProviderPaymentStatus.Success => payment with
            {
                Status = BillingPaymentStatus.NeedsReview,
                CompletedAt = now,
                StatusMessage = "The provider confirmed a payment with different details. Our team will review it.",
            },
            ProviderPaymentStatus.Failed => payment with { Status = BillingPaymentStatus.Failed, CompletedAt = now, StatusMessage = "The payment was declined." },
            ProviderPaymentStatus.Cancelled => payment with { Status = BillingPaymentStatus.Cancelled, CompletedAt = now, StatusMessage = "The payment was cancelled." },
            _ => payment,
        };

        if (payment.Status == BillingPaymentStatus.NeedsReview)
        {
            logger.LogWarning("Payment {PaymentId} verified as successful but amount, currency or reference did not match.", payment.Id);
        }

        await store.SavePaymentAsync(payment, cancellationToken);

        if (payment.Status == BillingPaymentStatus.Completed)
        {
            await ActivateAsync(payment, cancellationToken);
        }

        return payment;
    }

    // Renewals before expiry extend from the current period end, so no paid days are lost.
    private async Task ActivateAsync(BillingPayment payment, CancellationToken cancellationToken)
    {
        var plan = FindPlan(payment.PlanId) ?? throw new InvalidOperationException($"Plan '{payment.PlanId}' is no longer configured.");
        var now = timeProvider.GetUtcNow();
        var existing = await store.GetSubscriptionAsync(payment.UserId, cancellationToken);

        if (existing?.LastPaymentId == payment.Id)
        {
            return;
        }

        var start = existing is not null && existing.IsActive(now) && existing.PlanId == plan.Id ? existing.CurrentPeriodEnd : now;

        await store.SaveSubscriptionAsync(new Subscription
        {
            UserId = payment.UserId,
            PlanId = plan.Id,
            CurrentPeriodStart = existing is not null && existing.IsActive(now) && existing.PlanId == plan.Id ? existing.CurrentPeriodStart : now,
            CurrentPeriodEnd = start.AddDays(plan.PeriodDays),
            LastPaymentId = payment.Id,
            UpdatedAt = now,
        }, cancellationToken);

        logger.LogInformation("Activated plan {PlanId} for user {UserId} from payment {PaymentId}.", plan.Id, payment.UserId, payment.Id);
    }

    private BillingPlan? FindPlan(string? planId)
        => _billing.Plans.FirstOrDefault(plan => string.Equals(plan.Id, planId, StringComparison.OrdinalIgnoreCase));
}
