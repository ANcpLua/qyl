using OpenTelemetry;
using QylAttributes = Qyl.SemanticConventions.Attributes.Qyl.QylAttributes;

namespace Qyl.Instrumentation.Instrumentation.GenAi;

/// <summary>
///     Post-processor that reads <c>gen_ai.request.model</c> + token-usage tags from finished
///     spans, looks up per-token pricing in <see cref="QylPricingTable" />, and writes
///     <c>qyl.genai.cost_usd</c> + companions before the span is handed to the exporter.
/// </summary>
/// <remarks>
///     <para>
///         The processor is the SDK-side fast path. It runs in-process at every host that
///         calls <c>UseQyl()</c>, so cost is attached to the span at source — the collector
///         can read it without joining against a pricing table at query time. If the model is
///         not in the embedded pricing snapshot, <c>qyl.genai.cost_status</c> is set to
///         <c>unknown_model</c> and no cost tags are emitted; downstream tooling can join
///         against the collector's runtime <c>model_pricing</c> overrides instead.
///     </para>
///     <para>
///         Cost source-of-truth is the span attribute (per PRD #173). No counter metric is
///         emitted to avoid two write paths drifting.
///     </para>
/// </remarks>
public sealed class QylGenAiCostProcessor : BaseProcessor<Activity>
{
    public override void OnEnd(Activity data)
    {
        if (data is null) return;

        // Skip non-GenAI spans cheaply: no model tag => not a GenAI call.
        var model = TagAsString(data, "gen_ai.request.model")
                    ?? TagAsString(data, "gen_ai.response.model");
        if (string.IsNullOrEmpty(model)) return;

        var provider = TagAsString(data, "gen_ai.provider.name")
                       ?? TagAsString(data, "gen_ai.system");

        if (!QylPricingTable.TryGet(provider, model, out var entry))
        {
            data.SetTag(QylAttributes.GenaiCostStatus, QylAttributes.GenaiCostStatusValues.UnknownModel);
            return;
        }

        var inputTokens = TagAsInt64(data, "gen_ai.usage.input_tokens", "gen_ai.usage.prompt_tokens");
        var outputTokens = TagAsInt64(data, "gen_ai.usage.output_tokens", "gen_ai.usage.completion_tokens");

        if (inputTokens is null && outputTokens is null)
        {
            data.SetTag(QylAttributes.GenaiCostStatus, QylAttributes.GenaiCostStatusValues.MissingTokens);
            return;
        }

        var inputCost = (inputTokens ?? 0) * entry.InputCostPerToken;
        var outputCost = (outputTokens ?? 0) * entry.OutputCostPerToken;
        var total = inputCost + outputCost;

        data.SetTag(QylAttributes.GenaiCostInputUsd, inputCost);
        data.SetTag(QylAttributes.GenaiCostOutputUsd, outputCost);
        data.SetTag(QylAttributes.GenaiCostUsd, total);
        data.SetTag(QylAttributes.GenaiCostStatus, QylAttributes.GenaiCostStatusValues.Computed);
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
