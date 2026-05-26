using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Qyl.Collector.Auth;

namespace Qyl.Collector.Tests.Auth;

public sealed class TokenEncryptionTests
{
    private static readonly string ValidKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    [Fact]
    public void Encrypt_Decrypt_RoundtripsPlaintext()
    {
        var encryption = Create(ValidKey);
        var plaintext = "keycloak-refresh-token-payload"u8.ToArray();

        var envelope = encryption.Encrypt(plaintext);
        var decrypted = encryption.Decrypt(envelope);

        decrypted.Should().Equal(plaintext);
    }

    [Fact]
    public void Encrypt_ProducesDifferentCiphertextEachCall_ForSamePlaintext()
    {
        var encryption = Create(ValidKey);
        var plaintext = "deterministic-input"u8.ToArray();

        var first = encryption.Encrypt(plaintext);
        var second = encryption.Encrypt(plaintext);

        first.Should().NotEqual(second, "AES-GCM with a fresh nonce per call must produce distinct ciphertexts");
    }

    [Fact]
    public void Decrypt_RejectsTamperedCiphertext()
    {
        var encryption = Create(ValidKey);
        var envelope = encryption.Encrypt("sensitive"u8.ToArray());

        envelope[^1] ^= 0xFF;

        var act = () => encryption.Decrypt(envelope);

        act.Should().Throw<CryptographicException>("AES-GCM authentication tag must detect any byte modification");
    }

    [Fact]
    public void Decrypt_RejectsTamperedAuthTag()
    {
        var encryption = Create(ValidKey);
        var envelope = encryption.Encrypt("sensitive"u8.ToArray());

        // Tag sits in the [12..28] range right after the 12-byte nonce; flip a tag bit
        envelope[20] ^= 0x01;

        var act = () => encryption.Decrypt(envelope);

        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void Decrypt_RejectsEnvelopeShorterThanNonceAndTag()
    {
        var encryption = Create(ValidKey);
        var truncated = new byte[20];

        var act = () => encryption.Decrypt(truncated);

        act.Should().Throw<CryptographicException>().WithMessage("*Envelope is too short*");
    }

    [Fact]
    public void Constructor_ThrowsWhenKeyEnvVarMissing()
    {
        var act = static () => Create(key: null);

        act.Should().Throw<InvalidOperationException>().WithMessage("*QYL_TOKEN_ENCRYPTION_KEY is required*");
    }

    [Fact]
    public void Constructor_ThrowsWhenKeyIsNotBase64()
    {
        var act = static () => Create("not!base64!");

        act.Should().Throw<InvalidOperationException>().WithMessage("*must be a valid base64 string*");
    }

    [Fact]
    public void Constructor_ThrowsWhenKeyIsWrongLength()
    {
        var shortKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));

        var act = () => Create(shortKey);

        act.Should().Throw<InvalidOperationException>().WithMessage("*must decode to 32 bytes*");
    }

    private static ITokenEncryption Create(string? key) =>
        new AesGcmTokenEncryption(Options.Create(new TokenEncryptionOptions { Key = key }));
}
