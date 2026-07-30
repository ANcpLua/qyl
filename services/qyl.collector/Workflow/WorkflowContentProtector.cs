using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Qyl.Api.Contracts.Workflow;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Workflow;

internal sealed class WorkflowContentProtector
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[] _key;

    internal WorkflowContentProtector(byte[] key)
    {
        if (key.Length is not 32)
            throw new ArgumentException("Workflow content encryption key must contain 32 bytes.", nameof(key));
        _key = key.ToArray();
    }

    public static WorkflowContentProtector FromConfiguration(
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        if (configuration["QYL_WORKFLOW_CONTENT_KEY"] is { Length: > 0 } encoded)
        {
            byte[] key;
            try
            {
                key = Convert.FromBase64String(encoded);
            }
            catch (FormatException ex)
            {
                throw new InvalidOperationException(
                    "QYL_WORKFLOW_CONTENT_KEY must be a base64-encoded 32-byte key.",
                    ex);
            }
            return new WorkflowContentProtector(key);
        }

        if (environment.IsDevelopment() || environment.IsEnvironment("Testing"))
        {
            return new WorkflowContentProtector(
                SHA256.HashData(Encoding.UTF8.GetBytes("qyl-development-workflow-content-key")));
        }

        throw new InvalidOperationException(
            "Workflow content capture requires QYL_WORKFLOW_CONTENT_KEY (base64-encoded 32 bytes).");
    }

    private const string ContentRefPrefix = "sha256:";
    private const int ContentRefLength = 71;

    internal static void RequireWellFormedContentRef(string contentRef)
    {
        if (contentRef.Length != ContentRefLength ||
            !contentRef.StartsWith(ContentRefPrefix, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Captured content reference '{contentRef}' is not a well-formed 'sha256:' digest.");
        }

        foreach (var character in contentRef.AsSpan(ContentRefPrefix.Length))
        {
            if (character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f'))
            {
                throw new InvalidDataException(
                    $"Captured content reference '{contentRef}' is not lowercase hexadecimal.");
            }
        }
    }

    public WorkflowContentStorageRow Protect(WorkflowContentWrite content)
    {
        RequireWellFormedContentRef(content.ContentRef);
        var plaintext = Decode(content);
        var expected = content.ContentRef[ContentRefPrefix.Length..];
        var actual = Convert.ToHexStringLower(SHA256.HashData(plaintext));
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(expected),
                Encoding.ASCII.GetBytes(actual)))
        {
            throw new InvalidDataException(
                $"Captured content does not match its content reference '{content.ContentRef}'.");
        }

        var compressed = Compress(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[compressed.Length];
        var tag = new byte[TagSize];
        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, compressed, ciphertext, tag, Encoding.UTF8.GetBytes(content.ContentRef));
        return new WorkflowContentStorageRow(
            content.ContentRef,
            content.ContentType,
            content.Encoding,
            nonce,
            tag,
            ciphertext,
            plaintext.LongLength);
    }

    public string Unprotect(WorkflowContentStorageRow content)
    {
        var compressed = new byte[content.Ciphertext.Length];
        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(
            content.Nonce,
            content.Ciphertext,
            content.Tag,
            compressed,
            Encoding.UTF8.GetBytes(content.ContentRef));
        var plaintext = Decompress(compressed);
        if (plaintext.LongLength != content.SizeBytes)
            throw new InvalidDataException("Captured workflow content size does not match its authenticated metadata.");
        return content.Encoding is WorkflowContentEncoding.Base64
            ? Convert.ToBase64String(plaintext)
            : Encoding.UTF8.GetString(plaintext);
    }

    private static byte[] Decode(WorkflowContentWrite content) =>
        content.Encoding switch
        {
            WorkflowContentEncoding.Utf8 => Encoding.UTF8.GetBytes(content.Content),
            WorkflowContentEncoding.Base64 => DecodeBase64(content.Content),
            _ => throw new InvalidDataException($"Unsupported workflow content encoding '{content.Encoding}'.")
        };

    private static byte[] DecodeBase64(string content)
    {
        try
        {
            return Convert.FromBase64String(content);
        }
        catch (FormatException ex)
        {
            throw new WorkflowContentValidationException("Captured content is not valid base64.", ex);
        }
    }

    private static byte[] Compress(byte[] input)
    {
        using var output = new MemoryStream();
        using (var brotli = new BrotliStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
            brotli.Write(input);
        return output.ToArray();
    }

    private static byte[] Decompress(byte[] input)
    {
        using var source = new MemoryStream(input, writable: false);
        using var brotli = new BrotliStream(source, CompressionMode.Decompress);
        using var output = new MemoryStream();
        brotli.CopyTo(output);
        return output.ToArray();
    }
}

internal sealed class WorkflowContentValidationException(string message, Exception innerException)
    : Exception(message, innerException);
