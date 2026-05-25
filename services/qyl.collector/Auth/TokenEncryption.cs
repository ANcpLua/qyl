using Microsoft.Extensions.Options;

namespace Qyl.Collector.Auth;

public sealed class TokenEncryptionOptions
{
    public const string SectionName = "TokenEncryption";

    public const string KeyEnvVar = "QYL_TOKEN_ENCRYPTION_KEY";

    public string? Key { get; set; }
}

public interface ITokenEncryption
{
    byte[] Encrypt(ReadOnlySpan<byte> plaintext);

    byte[] Decrypt(ReadOnlySpan<byte> envelope);
}

internal sealed class AesGcmTokenEncryption : ITokenEncryption
{
    private const int NonceSize = 12;

    private const int TagSize = 16;

    private const int KeySize = 32;

    private readonly byte[] _key;

    public AesGcmTokenEncryption(IOptions<TokenEncryptionOptions> options)
    {
        var keyBase64 = options.Value.Key;

        if (string.IsNullOrWhiteSpace(keyBase64))
        {
            throw new InvalidOperationException(
                $"{TokenEncryptionOptions.KeyEnvVar} is required for MCP token encryption. " +
                "Generate with: openssl rand -base64 32");
        }

        try
        {
            _key = Convert.FromBase64String(keyBase64);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException(
                $"{TokenEncryptionOptions.KeyEnvVar} must be a valid base64 string.", ex);
        }

        if (_key.Length != KeySize)
        {
            throw new InvalidOperationException(
                $"{TokenEncryptionOptions.KeyEnvVar} must decode to {KeySize} bytes (got {_key.Length}). " +
                "Generate with: openssl rand -base64 32");
        }
    }

    public byte[] Encrypt(ReadOnlySpan<byte> plaintext)
    {
        var envelope = new byte[NonceSize + TagSize + plaintext.Length];
        var nonce = envelope.AsSpan(0, NonceSize);
        var tag = envelope.AsSpan(NonceSize, TagSize);
        var ciphertext = envelope.AsSpan(NonceSize + TagSize);

        RandomNumberGenerator.Fill(nonce);

        using var aesGcm = new AesGcm(_key, TagSize);
        aesGcm.Encrypt(nonce, plaintext, ciphertext, tag);

        return envelope;
    }

    public byte[] Decrypt(ReadOnlySpan<byte> envelope)
    {
        if (envelope.Length < NonceSize + TagSize)
        {
            throw new CryptographicException(
                $"Envelope is too short ({envelope.Length} bytes; minimum is {NonceSize + TagSize}).");
        }

        var nonce = envelope[..NonceSize];
        var tag = envelope.Slice(NonceSize, TagSize);
        var ciphertext = envelope[(NonceSize + TagSize)..];
        var plaintext = new byte[ciphertext.Length];

        using var aesGcm = new AesGcm(_key, TagSize);
        aesGcm.Decrypt(nonce, ciphertext, tag, plaintext);

        return plaintext;
    }
}
