using OpenTelemetry;

namespace Qyl.Instrumentation.Instrumentation.Inventory;

/// <summary>
///     Tracing-pipeline processor that updates <see cref="IQylAgentInventory" /> with
///     each observed agent invocation. Reads <c>gen_ai.agent.name</c> at <c>OnEnd</c>
///     and calls <see cref="IQylAgentInventory.RecordActivity" /> with the activity's
///     end timestamp. Spans without a <c>gen_ai.agent.name</c> tag are skipped at zero
///     cost; the inventory itself drops names that don't match a registration.
/// </summary>
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

        // Activity.StartTimeUtc is already UTC kind.
        var endUtc = data.StartTimeUtc + data.Duration;
        _inventory.RecordActivity(agentName, endUtc);
    }
}
