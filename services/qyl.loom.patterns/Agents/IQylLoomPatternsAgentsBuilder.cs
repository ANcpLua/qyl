// Copyright (c) 2025-2026 ancplua

namespace Qyl.Loom.Patterns.Agents;

/// <summary>
///     One factory method per bounded agent. Mirrors Apex's
///     <c>IExtractorAgentsBuilder</c> — each call returns a fully composed
///     <see cref="AIAgent" /> wrapped with <c>UseQylAgentTelemetry()</c>, so
///     <c>QYL0135</c> is satisfied at the construction site.
/// </summary>
public interface IQylLoomPatternsAgentsBuilder
{
    /// <summary>Stage 1 — synthesizes a root-cause hypothesis from a raw signal.</summary>
    AIAgent BuildRcaAgent();

    /// <summary>Stage 2 — turns an RCA into an actionable solution plan.</summary>
    AIAgent BuildSolutionAgent();

    /// <summary>Stage 3 — assigns a confidence verdict (approved / rejected).</summary>
    AIAgent BuildConfidenceAgent();
}
