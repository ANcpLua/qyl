
using Microsoft.Extensions.Compliance.Classification;
using Microsoft.Extensions.Compliance.Redaction;

namespace Qyl.Collector.Telemetry;

internal static class QylDataClassification
{
    public static DataClassification Pii => new("PII", "GDPR:PersonalData");

    public static DataClassification Secret => new("Secret", "Security:Credential");

    public static DataClassification PromptContent => new("PromptContent", "GenAI:UserContent");

    public static DataClassification InternalId => new("InternalId", "Internal:Identifier");
}

internal static class QylRedactionExtensions
{
    public static IRedactionBuilder AddQylRedactors(this IRedactionBuilder builder)
    {
        builder.SetRedactor<ErasingRedactor>(QylDataClassification.Pii);

        builder.SetRedactor<ErasingRedactor>(QylDataClassification.Secret);

        builder.SetRedactor<ErasingRedactor>(QylDataClassification.PromptContent);

        builder.SetRedactor<NullRedactor>(QylDataClassification.InternalId);

        return builder;
    }
}
