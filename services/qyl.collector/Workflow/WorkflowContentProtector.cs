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

        // Deriving this key from QYL_OTLP_PRIMARY_API_KEY used to be the production fallback.
        // That silently bound the lifetime of stored content to a ROTATABLE ingest credential:
        // rotating the OTLP key left every previously captured payload undecryptable, as an
        // AES-GCM tag mismatch at read time rather than anything that looks like a key problem.
        // Content encryption needs a key with its own rotation story, so refuse to start rather
        // than accept one that is guaranteed to be rotated out from under the data.
        throw new InvalidOperationException(
            "Workflow content capture requires QYL_WORKFLOW_CONTENT_KEY (base64-encoded 32 bytes). " +
            "It must not be derived from the OTLP ingest key: that key rotates, and rotating it " +
            "would permanently destroy access to all previously captured workflow content.");
    }

    private const string ContentRefPrefix = "sha256:";
    private const int ContentRefLength = 71; // "sha256:" + 64 lowercase hex characters.

    /// <summary>
    /// The <c>^sha256:[a-f0-9]{64}$</c> pattern on WorkflowContentRef is an OpenAPI constraint;
    /// the generated contract carries no runtime validation attribute, so an untrusted observer
    /// can post any string. Without this guard a ref shorter than the prefix threw
    /// ArgumentOutOfRangeException out of the slice below and surfaced as a 500 from the append
    /// endpoint instead of a rejected request.
    /// </summary>
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
            WorkflowContentEncoding.Base64 => Convert.FromBase64String(content.Content),
            _ => throw new InvalidDataException($"Unsupported workflow content encoding '{content.Encoding}'.")
        };

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
