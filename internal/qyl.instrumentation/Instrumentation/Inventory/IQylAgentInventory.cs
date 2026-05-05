namespace Qyl.Instrumentation.Instrumentation.Inventory;

public interface IQylAgentInventory
{
    void Register(AgentRegistration registration);

    void RecordActivity(string agentName, DateTime timestamp);

    IReadOnlyList<AgentRegistration> Snapshot();
}

public sealed record AgentRegistration(
    string Key,
    string Name,
    string? Description,
    string? InstructionsHash,
    string? ProviderName,
    DateTime RegisteredAtUtc)
{
    public DateTime? LastSeenUtc { get; init; }

    public long CallCount24h { get; init; }
}
