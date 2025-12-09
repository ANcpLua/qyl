// qyl.collector - Token Generator
// Cryptographically secure token generation

using System.Security.Cryptography;

namespace qyl.collector.Auth;

/// <summary>
/// Generates cryptographically secure tokens.
/// </summary>
public static class TokenGenerator
{
    /// <summary>
    /// Generates a URL-safe random token.
    /// Uses base64url encoding (RFC 4648) without padding.
    /// </summary>
    /// <param name="byteLength">Number of random bytes (default 24 = 32 chars)</param>
    /// <returns>URL-safe token string</returns>
    public static string Generate(int byteLength = 24)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(byteLength);

        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(byteLength))
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }
}
