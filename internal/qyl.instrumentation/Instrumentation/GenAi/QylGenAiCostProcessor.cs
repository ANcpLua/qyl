using OpenTelemetry;

namespace Qyl.Instrumentation.Instrumentation.GenAi;

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
