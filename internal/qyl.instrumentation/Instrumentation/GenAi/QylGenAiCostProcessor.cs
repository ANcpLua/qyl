using OpenTelemetry;
using GenAiAttributes = Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.GenAi.GenAiAttributes;

namespace Qyl.Instrumentation.Instrumentation.GenAi;

public sealed class QylGenAiCostProcessor : BaseProcessor<Activity>
{
    public const string CostAttribute = "gen_ai.usage.cost";

    public override void OnEnd(Activity data)
    {
        if (data is null) return;

        var model = TagAsString(data, GenAiAttributes.RequestModel)
                    ?? TagAsString(data, GenAiAttributes.ResponseModel);
        if (string.IsNullOrEmpty(model)) return;

        var provider = TagAsString(data, GenAiAttributes.ProviderName);

        if (!QylPricingTable.TryGet(provider, model, out var entry))
            return;

        var inputTokens = TagAsInt64(data, GenAiAttributes.UsageInputTokens);
        var outputTokens = TagAsInt64(data, GenAiAttributes.UsageOutputTokens);

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

    private static long? TagAsInt64(Activity activity, string key)
    {
        var raw = activity.GetTagItem(key);
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
