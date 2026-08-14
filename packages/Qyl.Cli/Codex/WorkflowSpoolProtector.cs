using System.Security.Cryptography;
using System.Text;

namespace Qyl.Cli.Codex;

internal sealed class WorkflowSpoolProtector
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[] _key;

    private WorkflowSpoolProtector(byte[] key)
    {
        _key = key;
    }

    public static WorkflowSpoolProtector Open(string root)
    {
        Directory.CreateDirectory(root);
        var keyPath = Path.Combine(root, "spool.key");
        byte[] key;
        try
        {
            key = File.ReadAllBytes(keyPath);
        }
        catch (FileNotFoundException)
        {
            key = RandomNumberGenerator.GetBytes(32);
            try
            {
                using var stream = new FileStream(
                    keyPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    32,
                    FileOptions.WriteThrough);
                stream.Write(key);
                stream.Flush(flushToDisk: true);
                RestrictToCurrentUser(keyPath);
            }
            catch (IOException)
            {
                key = File.ReadAllBytes(keyPath);
            }
        }

        if (key.Length is not 32)
            throw new InvalidDataException($"Workflow spool key '{keyPath}' must contain exactly 32 bytes.");
        RestrictToCurrentUser(keyPath);
        return new WorkflowSpoolProtector(key);
    }

    public WorkflowSpoolEnvelope Protect(ReadOnlySpan<byte> plaintext)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var tag = new byte[TagSize];
        var ciphertext = new byte[plaintext.Length];
        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plaintext, ciphertext, tag, Encoding.UTF8.GetBytes("qyl-codex-spool-v1"));
        return new WorkflowSpoolEnvelope(
            Convert.ToBase64String(nonce),
            Convert.ToBase64String(tag),
            Convert.ToBase64String(ciphertext));
    }

    public byte[] Unprotect(WorkflowSpoolEnvelope envelope)
    {
        var nonce = Convert.FromBase64String(envelope.Nonce);
        var tag = Convert.FromBase64String(envelope.Tag);
        var ciphertext = Convert.FromBase64String(envelope.Ciphertext);
        var plaintext = new byte[ciphertext.Length];
        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(
            nonce,
            ciphertext,
            tag,
            plaintext,
            Encoding.UTF8.GetBytes("qyl-codex-spool-v1"));
        return plaintext;
    }

    public string KeyedDigest(ReadOnlySpan<byte> value)
    {
        var digest = HMACSHA256.HashData(_key, value);
        return $"hmac-sha256:{Convert.ToHexStringLower(digest)}";
    }

    internal static void RestrictToCurrentUser(string path)
    {
        if (OperatingSystem.IsWindows())
            return;
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }
}
