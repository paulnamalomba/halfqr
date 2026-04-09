using System.Security.Cryptography;
using System.Text;

namespace HaveQR.QrEngine.Hashing;

public sealed class Sha256HashService : IHashService
{
    public string Compute(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}