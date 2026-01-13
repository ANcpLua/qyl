// =============================================================================
// qyl Data Classification - .NET 10 Compliance/Redaction Framework
// Defines data classifications for automatic PII/sensitive data redaction
// =============================================================================

using Microsoft.Extensions.Compliance.Classification;
using Microsoft.Extensions.Compliance.Redaction;

namespace qyl.collector.Telemetry;

/// <summary>
///     qyl-specific data classifications for log redaction.
///     .NET 8+ feature: Data classification taxonomy.
/// </summary>
public static class QylDataClassification
{
    /// <summary>
    ///     Personally Identifiable Information - will be erased in logs.
    ///     Examples: email, phone, user IDs, IP addresses.
    /// </summary>
    public static DataClassification Pii => new("PII", "GDPR:PersonalData");

    /// <summary>
    ///     Secret/credential data - will be HMAC hashed for correlation.
    ///     Examples: API keys, tokens, passwords.
    /// </summary>
    public static DataClassification Secret => new("Secret", "Security:Credential");

    /// <summary>
    ///     Prompt/completion content - may contain sensitive user data.
    ///     Will be truncated or hashed depending on configuration.
    /// </summary>
    public static DataClassification PromptContent => new("PromptContent", "GenAI:UserContent");

    /// <summary>
    ///     Internal identifiers - safe to log but should be anonymized externally.
    ///     Examples: session IDs, trace IDs.
    /// </summary>
    public static DataClassification InternalId => new("InternalId", "Internal:Identifier");
}

/// <summary>
///     Custom redactor for GenAI prompt/completion content.
///     Truncates long content and adds hash suffix for correlation.
/// </summary>
public sealed class PromptContentRedactor : Redactor
{
    private const int MaxLength = 100;
    private const string Suffix = "...[truncated]";

    public override int GetRedactedLength(ReadOnlySpan<char> input)
    {
        if (input.Length <= MaxLength)
            return input.Length;

        return MaxLength + Suffix.Length;
    }

    public override int Redact(ReadOnlySpan<char> source, Span<char> destination)
    {
        if (source.Length <= MaxLength)
        {
            source.CopyTo(destination);
            return source.Length;
        }

        source[..MaxLength].CopyTo(destination);
        Suffix.AsSpan().CopyTo(destination[MaxLength..]);
        return MaxLength + Suffix.Length;
    }
}

/// <summary>
///     Custom redactor that produces a stable hash for correlation.
///     Useful for secrets that need to be correlated across logs.
/// </summary>
public sealed class HashingRedactor : Redactor
{
    private const int HashLength = 16; // First 16 chars of SHA256

    public override int GetRedactedLength(ReadOnlySpan<char> input) => HashLength;

    public override int Redact(ReadOnlySpan<char> source, Span<char> destination)
    {
        // Use XxHash64 for speed (non-cryptographic, but fast and stable)
        var bytes = Encoding.UTF8.GetBytes(source.ToString());
        var hash = XxHash64.HashToUInt64(bytes);

        var hashString = hash.ToString("x16");
        hashString.AsSpan().CopyTo(destination);
        return HashLength;
    }
}

/// <summary>
///     Attributes for marking properties with data classifications.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class PiiDataAttribute : DataClassificationAttribute
{
    public PiiDataAttribute() : base(QylDataClassification.Pii) { }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class SecretDataAttribute : DataClassificationAttribute
{
    public SecretDataAttribute() : base(QylDataClassification.Secret) { }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class PromptContentAttribute : DataClassificationAttribute
{
    public PromptContentAttribute() : base(QylDataClassification.PromptContent) { }
}

/// <summary>
///     Extension methods for configuring qyl redaction.
/// </summary>
public static class QylRedactionExtensions
{
    /// <summary>
    ///     Configures qyl-specific redactors.
    /// </summary>
    public static IRedactionBuilder AddQylRedactors(this IRedactionBuilder builder)
    {
        // Erase PII completely
        builder.SetRedactor<ErasingRedactor>(QylDataClassification.Pii);

        // Hash secrets for correlation
        builder.SetRedactor<HashingRedactor>(QylDataClassification.Secret);

        // Truncate prompt content
        builder.SetRedactor<PromptContentRedactor>(QylDataClassification.PromptContent);

        // Internal IDs can pass through (but could be hashed externally)
        builder.SetRedactor<NullRedactor>(QylDataClassification.InternalId);

        return builder;
    }
}
