using System.Security.Cryptography;

namespace qyl.collector.Auth;

public static class TokenGenerator
{
    public static string Generate(int byteLength = 24)
    {
        Throw.Throw.IfNegativeOrZero(byteLength);

        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(byteLength))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}