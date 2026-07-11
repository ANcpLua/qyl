namespace Qyl.Collector.Ingestion;

/// <summary>
///     Ingestion-time normalization of deprecated OTel attribute keys to their canonical
///     successors, per the contract in qyl-api-schema <c>VERSIONING.md</c> ("Ingestion mapping
///     (deprecated -&gt; current)"). Telemetry from older SDKs still carries the old keys; the
///     capture allow-lists are generated from the current semconv registry, so without this
///     rename the old keys would be silently dropped at ingest. Runs BEFORE the allow-list
///     check; when both the old and the canonical key are present, the canonical value wins.
///     Keys are renamed, values pass through untouched in their original AnyValue shape.
/// </summary>
internal static class DeprecatedAttributeNormalizer
{
    /// <summary>
    ///     Returns the canonical key for <paramref name="key" /> and whether it was renamed.
    ///     A renamed key must not overwrite a value already present under the canonical key.
    /// </summary>
    internal static bool TryNormalize(string key, out string canonical)
    {
        var mapped = key switch
        {
            "gen_ai.system" => "gen_ai.provider.name",
            "gen_ai.usage.prompt_tokens" => "gen_ai.usage.input_tokens",
            "gen_ai.usage.completion_tokens" => "gen_ai.usage.output_tokens",
            "agents.tool.call_id" => "gen_ai.tool.call.id",
            "db.system" => "db.system.name",
            _ => null
        };

        canonical = mapped ?? key;
        return mapped is not null;
    }
}
