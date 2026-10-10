namespace HalfQR.Billing.Models;

public enum BillingPaymentStatus
{
    Pending,
    Completed,
    Failed,
    Cancelled,
    Expired,
    // Provider reported success but amount, currency or reference did not match. Needs manual review.
    NeedsReview,
}

public sealed record BillingPayment
{
    public Guid Id { get; init; }

    public Guid UserId { get; init; }

    public string PlanId { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public string Currency { get; init; } = "MWK";

    public string Operator { get; init; } = string.Empty;

    // Only the last 4 digits are kept for display.
    public string MobileLastFour { get; init; } = string.Empty;

    public string TxRef { get; init; } = string.Empty;

    public string? ProviderChargeId { get; init; }

    public string? ProviderReferenceId { get; init; }

    public BillingPaymentStatus Status { get; init; } = BillingPaymentStatus.Pending;

    public string? StatusMessage { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? CompletedAt { get; init; }

    public DateTimeOffset? LastVerifiedAt { get; init; }

    public bool IsTerminal
        => Status is not BillingPaymentStatus.Pending;
}
