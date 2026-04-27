// Copyright (c) 2025-2026 ancplua

using Microsoft.Agents.AI;
using Qyl.Loom.Autofix;

namespace Qyl.Loom.Agents;

/// <summary>
///     One factory method per bounded qyl.loom agent. Mirrors Apex's
///     <c>IExtractorAgentsBuilder</c> — each call returns a fully composed
///     <see cref="AIAgent" /> wrapped with <c>UseQylAgentTelemetry()</c>, so
///     <c>QYL0135</c> is satisfied at the construction site.
/// </summary>
/// <remarks>
///     <para>
///         <see cref="IsConfigured" /> reflects whether the upstream
///         <see cref="Qyl.Loom.Clients.IQylLoomChatClientBuilder" /> resolved a
///         non-null <see cref="IChatClient" />. Each <c>Build*Agent</c> method
///         throws <see cref="InvalidOperationException" /> when
///         <see cref="IsConfigured" /> is <see langword="false" /> — callers
///         gate the call on the property and fall back to the no-LLM path.
///     </para>
///     <para>
///         The structured-output autofix agent
///         (<see cref="BuildAutofixAgent" />) returns an agent whose
///         <c>ChatOptions.ResponseFormat</c> is locked to
///         <see cref="AutofixReport" />; callers invoke
///         <c>agent.RunAsync&lt;AutofixReport&gt;(...)</c>.
///     </para>
/// </remarks>
public interface IQylLoomAgentsBuilder
{
    /// <summary>
    ///     <see langword="true" /> when an LLM provider is configured and the
    ///     <c>Build*Agent</c> methods can return non-null instances.
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>Triage stage — scores fixability and proposes an automation level for an issue summary.</summary>
    AIAgent BuildTriageScoringAgent();

    /// <summary>Code-review stage — reviews a PR diff and emits structured JSON comments.</summary>
    AIAgent BuildCodeReviewAgent();

    /// <summary>Autofix stage — single-agent five-stage contract with schema-enforced <see cref="AutofixReport" /> output.</summary>
    AIAgent BuildAutofixAgent();

    /// <summary>Exploration stage — produces a pre-investigation insight summary (what happened / initial guess / in the trace).</summary>
    AIAgent BuildExplorationInsightAgent();

    /// <summary>Exploration stage — converts a root-cause analysis into a minimal implementation plan.</summary>
    AIAgent BuildExplorationStrategistAgent();
}
