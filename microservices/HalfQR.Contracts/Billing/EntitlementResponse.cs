namespace HalfQR.Contracts.Billing;

// What a user's current plan allows. Identity reads it before issuing or accepting API credentials.
public sealed record EntitlementResponse
{
    public string PlanId { get; init; } = "free";

    public string PlanName { get; init; } = "Free";

    public bool IsPaid { get; init; }

    public DateTimeOffset? ActiveUntil { get; init; }

    public int MaxApiKeys { get; init; }

    public int MaxAccessTokens { get; init; }

    public int RequestsPerMinute { get; init; }

    public static EntitlementResponse Free { get; } = new();
}
