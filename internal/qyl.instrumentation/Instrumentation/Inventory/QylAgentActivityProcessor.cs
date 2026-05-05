using OpenTelemetry;

namespace Qyl.Instrumentation.Instrumentation.Inventory;

public sealed class QylAgentActivityProcessor : BaseProcessor<Activity>
{
    private readonly IQylAgentInventory _inventory;

    public QylAgentActivityProcessor(IQylAgentInventory inventory) =>
        _inventory = Guard.NotNull(inventory);

    public override void OnEnd(Activity data)
    {
        if (data is null) return;

        if (data.GetTagItem("gen_ai.agent.name") is not string agentName || agentName.Length == 0)
            return;

        var endUtc = data.StartTimeUtc + data.Duration;
        _inventory.RecordActivity(agentName, endUtc);
    }
}
