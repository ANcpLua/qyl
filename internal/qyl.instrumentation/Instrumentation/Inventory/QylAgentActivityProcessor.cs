using OpenTelemetry;
using GenAiAttributes = Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.GenAi.GenAiAttributes;

namespace Qyl.Instrumentation.Instrumentation.Inventory;

public sealed class QylAgentActivityProcessor : BaseProcessor<Activity>
{
    private readonly IQylAgentInventory _inventory;

    public QylAgentActivityProcessor(IQylAgentInventory inventory) =>
        _inventory = Guard.NotNull(inventory);

    public override void OnEnd(Activity data)
    {
        if (data is null) return;

        if (data.GetTagItem(GenAiAttributes.AgentName) is not string agentName || agentName.Length is 0)
            return;

        var endUtc = data.StartTimeUtc + data.Duration;
        _inventory.RecordActivity(agentName, endUtc);
    }
}
