using System.Security.Cryptography;
using System.Text;

namespace HalfQR.Identity.Security;

internal static class SecretTokens
{
    public const string ApiKeyPrefix = "hqr_sk_";
    public const string AccessTokenPrefix = "hqr_pat_";
    public const string SessionPrefix = "hqr_sess_";

    private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
    private const int SecretLength = 40;

    // 40 base62 characters is ~238 bits of entropy, so a fast hash is enough for lookup.
    public static string Create(string prefix)
        => prefix + RandomNumberGenerator.GetString(Alphabet, SecretLength);

    public static string Hash(string token)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public static bool FixedTimeEquals(string left, string right)
        => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(left), Encoding.UTF8.GetBytes(right));

    // Shown in the dashboard so users can tell keys apart without the secret.
    public static string DisplayPrefix(string token)
        => token[..Math.Min(token.Length, token.IndexOf('_', 4) + 5)];
}
