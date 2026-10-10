using HalfQR.Billing.Models;
using HalfQR.Contracts.Billing;

namespace HalfQR.Billing;

public sealed record CheckoutRequest(string? PlanId, string? Operator, string? PhoneNumber);

public sealed record PlanResponse(string Id, string Name, string Description, decimal AmountMwk, int PeriodDays, int MaxApiKeys, int MaxAccessTokens, int RequestsPerMinute, bool Highlighted, IReadOnlyList<string> Features)
{
    public static PlanResponse From(BillingPlan plan)
        => new(plan.Id, plan.Name, plan.Description, plan.AmountMwk, plan.PeriodDays, plan.MaxApiKeys, plan.MaxAccessTokens, plan.RequestsPerMinute, plan.Highlighted, plan.Features);
}

public sealed record OperatorResponse(string Id, string Label, string NumberPrefix);

public sealed record PaymentResponse(
    Guid Id,
    string PlanId,
    decimal Amount,
    string Currency,
    string Operator,
    string MobileLastFour,
    string TxRef,
    BillingPaymentStatus Status,
    string? StatusMessage,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt)
{
    public static PaymentResponse From(BillingPayment payment)
        => new(payment.Id, payment.PlanId, payment.Amount, payment.Currency, payment.Operator, payment.MobileLastFour, payment.TxRef, payment.Status, payment.StatusMessage, payment.CreatedAt, payment.CompletedAt);
}

public sealed record BillingOverviewResponse(
    EntitlementResponse Entitlement,
    IReadOnlyList<PlanResponse> Plans,
    IReadOnlyList<OperatorResponse> Operators,
    IReadOnlyList<PaymentResponse> Payments,
    bool MockMode);
