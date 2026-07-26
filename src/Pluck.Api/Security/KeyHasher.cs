using System.Security.Cryptography;
using System.Text;

namespace Pluck.Api.Security;

/// <summary>
/// Static class for hashing api key
/// </summary>
public static class KeyHasher
{
    public static string ComputeHash(string rawKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawKey));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}