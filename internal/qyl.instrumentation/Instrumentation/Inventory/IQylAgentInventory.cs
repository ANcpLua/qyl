namespace Qyl.Instrumentation.Instrumentation.Inventory;

/// <summary>
///     Cache of agent registrations populated at construction time. Surface for the
///     Sentry-parity "Naming Your Agents" panel — what agents exist, what their
///     instructions hash to, when they were last seen building.
/// </summary>
/// <remarks>
///     <para>
///         The inventory is populated by direct <see cref="Register" /> calls — the qyl
///         three-builder pattern (chat-client → agents-builder → workflow) hands the agent
///         to <c>RegisterAgent</c> right after the <c>.AsBuilder().UseQylAgentTelemetry().Build()</c>
///         wrap. Walking <see cref="IServiceProvider" /> for keyed agents is intentionally
///         avoided — <c>IKeyedServiceProvider</c> exposes no plural enumeration across keys.
///     </para>
///     <para>
///         Calls are idempotent on <c>Key</c> — repeated registrations refresh the
///         <c>RegisteredAtUtc</c> stamp and overwrite metadata, which is the right semantic
///         for tracking instructions-hash drift across sessions.
///     </para>
/// </remarks>
public interface IQylAgentInventory
{
    /// <summary>Register or refresh an agent's metadata.</summary>
    void Register(AgentRegistration registration);

    /// <summary>Snapshot the current set of registrations. The returned list is independent of the live cache.</summary>
    IReadOnlyList<AgentRegistration> Snapshot();
}

/// <summary>
///     Single registration entry exposed to admin endpoints and the dashboard. Hash is
///     emitted as uppercase hex SHA256 (no salt — goal is cross-deployment diff-ability,
///     not secrecy).
/// </summary>
public sealed record AgentRegistration(
    string Key,
    string Name,
    string? Description,
    string? InstructionsHash,
    string? ProviderName,
    DateTime RegisteredAtUtc);
