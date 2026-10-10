namespace HalfQR.Identity;

public sealed class IdentityOptions
{
    public const string FileSystemProvider = "FileSystem";
    public const string PostgreSqlProvider = "PostgreSql";

    // Secret PublicApi sends to /internal endpoints. Empty disables them.
    public string InternalApiKey { get; set; } = string.Empty;

    // Allows passwordless sign-in with any email. Development only.
    public bool EnableDevSignIn { get; set; }

    public int SessionLifetimeDays { get; set; } = 7;

    // Public webapp origin used to build email verification links.
    public string WebappBaseUrl { get; set; } = "http://localhost:3000";

    // HalfQR.Billing base URL. API key issuance and verification require an active paid plan from Billing.
    public string BillingBaseUrl { get; set; } = "http://localhost:5166";

    public int MaxActiveCredentialsPerKind { get; set; } = 25;

    public string StoreProvider { get; set; } = FileSystemProvider;

    public string FileSystemPath { get; set; } = ".data/identity/identity.json";

    public string PostgresConnectionString { get; set; } = string.Empty;

    public string PostgresSchema { get; set; } = "auth";
}

public sealed class GoogleOAuthOptions
{
    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    // Exact redirect URIs the webapp may use. The code exchange rejects anything else.
    public string[] AllowedRedirectUris { get; set; } = [];

    public bool IsConfigured
        => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}
