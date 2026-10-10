namespace HalfQR.Billing;

public sealed class BillingOptions
{
    public const string FileSystemProvider = "FileSystem";
    public const string PostgreSqlProvider = "PostgreSql";

    // Secret Identity sends to /internal endpoints. Empty disables them.
    public string InternalApiKey { get; set; } = string.Empty;

    public string StoreProvider { get; set; } = FileSystemProvider;

    public string FileSystemPath { get; set; } = ".data/billing/billing.json";

    public string PostgresConnectionString { get; set; } = string.Empty;

    public string PostgresSchema { get; set; } = "billing";

    // Pending mobile-money charges older than this are verified one last time, then expired.
    public int PendingPaymentTimeoutMinutes { get; set; } = 15;

    public List<BillingPlan> Plans { get; set; } = [];
}

public sealed class BillingPlan
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public decimal AmountMwk { get; set; }

    public int PeriodDays { get; set; } = 30;

    public int MaxApiKeys { get; set; }

    public int MaxAccessTokens { get; set; }

    public int RequestsPerMinute { get; set; }

    public bool Highlighted { get; set; }

    public List<string> Features { get; set; } = [];
}

public sealed class PayChanguOptions
{
    public string SecretKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://api.paychangu.com";

    // HMAC-SHA256 secret for the "Signature" webhook header. Required in Production.
    public string WebhookSecret { get; set; } = string.Empty;

    public string WebhookUrl { get; set; } = string.Empty;

    // Simulates PayChangu. Refused in Production.
    public bool MockMode { get; set; }

    public string Currency { get; set; } = "MWK";

    public Dictionary<string, MobileMoneyOperator> Operators { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class MobileMoneyOperator
{
    public string OperatorId { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    // Malawi numbers are 9 digits after +265; Airtel starts with 9, TNM with 8.
    public string NumberPrefix { get; set; } = string.Empty;
}
