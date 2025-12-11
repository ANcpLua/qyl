using System.Security.Cryptography;
using Qyl;

namespace qyl.collector.Auth;

public static class TokenGenerator
{
    public static string Generate(int byteLength = 24)
    {
        Throw.IfLessThan(byteLength, 1);

        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(byteLength))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
