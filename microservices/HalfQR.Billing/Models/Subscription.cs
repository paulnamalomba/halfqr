namespace HalfQR.Billing.Models;

public sealed record Subscription
{
    public Guid UserId { get; init; }

    public string PlanId { get; init; } = string.Empty;

    public DateTimeOffset CurrentPeriodStart { get; init; }

    public DateTimeOffset CurrentPeriodEnd { get; init; }

    public Guid LastPaymentId { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }

    public bool IsActive(DateTimeOffset now)
        => CurrentPeriodEnd > now;
}
