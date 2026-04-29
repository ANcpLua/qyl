using OpenTelemetry;

namespace Qyl.Instrumentation.Instrumentation.GenAi;

/// <summary>
///     Post-processor that reads <c>gen_ai.request.model</c> + token-usage tags from finished
///     spans, looks up per-token pricing in <see cref="QylPricingTable" />, and writes
///     <c>gen_ai.usage.cost</c> before the span is handed to the exporter.
/// </summary>
/// <remarks>
///     <para>
///         Cost is the SDK-side fast path: it runs in-process at every host that calls
///         <c>UseQyl()</c>, so the span carries cost at source — the collector reads the
///         attribute via <see cref="OtlpConverter" /> without joining against a pricing
///         table at query time. Unknown models and missing token counts are simply
///         dropped (no cost attribute emitted) — the collector falls back to its
///         server-side <c>ModelPricingService</c> for those.
///     </para>
///     <para>
///         Only the canonical-style <c>gen_ai.usage.cost</c> attribute is emitted; no
///         qyl-namespaced cost attributes are introduced (they would shadow the upstream
///         convention).
///     </para>
/// </remarks>
public sealed class QylGenAiCostProcessor : BaseProcessor<Activity>
{
    public const string CostAttribute = "gen_ai.usage.cost";

    public override void OnEnd(Activity data)
    {
        if (data is null) return;

        var model = TagAsString(data, "gen_ai.request.model")
                    ?? TagAsString(data, "gen_ai.response.model");
        if (string.IsNullOrEmpty(model)) return;

        var provider = TagAsString(data, "gen_ai.provider.name")
                       ?? TagAsString(data, "gen_ai.system");

        if (!QylPricingTable.TryGet(provider, model, out var entry))
            return;

        var inputTokens = TagAsInt64(data, "gen_ai.usage.input_tokens", "gen_ai.usage.prompt_tokens");
        var outputTokens = TagAsInt64(data, "gen_ai.usage.output_tokens", "gen_ai.usage.completion_tokens");

        if (inputTokens is null && outputTokens is null)
            return;

        var total = (inputTokens ?? 0) * entry.InputCostPerToken
                    + (outputTokens ?? 0) * entry.OutputCostPerToken;

        data.SetTag(CostAttribute, total);
    }

    private static string? TagAsString(Activity activity, string key) =>
        activity.GetTagItem(key) switch
        {
            string s when s.Length > 0 => s,
            _ => null
        };

    private static long? TagAsInt64(Activity activity, string primary, string fallback)
    {
        var raw = activity.GetTagItem(primary) ?? activity.GetTagItem(fallback);
        return raw switch
        {
            long l => l,
            int i => i,
            short sh => sh,
            byte b => b,
            string s when long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) =>
                parsed,
            _ => null
        };
    }
}
