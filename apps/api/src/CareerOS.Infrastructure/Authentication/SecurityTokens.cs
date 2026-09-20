using System.Security.Cryptography;
using System.Text;

namespace CareerOS.Infrastructure.Authentication;

public static class SecurityTokens
{
    public static string Create() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    public static bool IsWellFormed(string? token) => token is { Length: 64 } && token.All(Uri.IsHexDigit);
    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
