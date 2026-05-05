
using Microsoft.Extensions.Compliance.Classification;
using Microsoft.Extensions.Compliance.Redaction;

namespace Qyl.Collector.Telemetry;

public static class QylDataClassification
{
    public static DataClassification Pii => new("PII", "GDPR:PersonalData");

    public static DataClassification Secret => new("Secret", "Security:Credential");

    public static DataClassification PromptContent => new("PromptContent", "GenAI:UserContent");

    public static DataClassification InternalId => new("InternalId", "Internal:Identifier");
}

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

public sealed class HashingRedactor : Redactor
{
    private const int HashLength = 16;

    public override int GetRedactedLength(ReadOnlySpan<char> input) => HashLength;

    public override int Redact(ReadOnlySpan<char> source, Span<char> destination)
    {
        var bytes = Encoding.UTF8.GetBytes(source.ToString());
        var hash = XxHash64.HashToUInt64(bytes);

        var hashString = hash.ToString("x16");
        hashString.AsSpan().CopyTo(destination);
        return HashLength;
    }
}

public static class QylRedactionExtensions
{
    public static IRedactionBuilder AddQylRedactors(this IRedactionBuilder builder)
    {
        builder.SetRedactor<ErasingRedactor>(QylDataClassification.Pii);

        builder.SetRedactor<HashingRedactor>(QylDataClassification.Secret);

        builder.SetRedactor<PromptContentRedactor>(QylDataClassification.PromptContent);

        builder.SetRedactor<NullRedactor>(QylDataClassification.InternalId);

        return builder;
    }
}
