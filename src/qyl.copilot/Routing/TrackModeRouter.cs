// =============================================================================
// qyl.copilot - TrackMode Router
// Unified tri-track routing for Creative / Reasoning / Enterprise execution.
// =============================================================================

using qyl.protocol.Copilot;

namespace qyl.copilot.Routing;

/// <summary>
///     Routing decision emitted by <see cref="TrackModeRouter"/>.
/// </summary>
public sealed record TrackRouteDecision
{
    /// <summary>Mode requested by the caller.</summary>
    public required TrackMode RequestedMode { get; init; }

    /// <summary>Mode selected by the router.</summary>
    public required TrackMode EffectiveMode { get; init; }

    /// <summary>Optional explanation for the selected mode.</summary>
    public string? Reason { get; init; }
}

/// <summary>
///     Classifies requests into tri-track execution modes and provides
///     mode-specific prompt/workflow helpers.
/// </summary>
public static class TrackModeRouter
{
    private static readonly string[] s_enterpriseKeywords =
    [
        "approval", "audit", "compliance", "control", "enterprise", "gdpr", "governance",
        "hipaa", "legal", "m365", "oauth", "policy", "rbac", "risk", "soc2", "tenant", "teams"
    ];

    private static readonly string[] s_reasoningKeywords =
    [
        "analyze", "analysis", "blast radius", "compare", "confidence", "diagnose",
        "evidence", "hypothesis", "investigate", "regression", "root cause", "triage", "verify", "why"
    ];

    private static readonly string[] s_creativeKeywords =
    [
        "brainstorm", "campaign", "copy", "creative", "headline", "idea",
        "narrative", "name", "remix", "story", "tagline", "write"
    ];

    /// <summary>
    ///     Resolves an effective mode.
    ///     Explicit non-auto modes are respected as-is.
    ///     Auto mode classifies only on strong signals; otherwise remains Auto.
    /// </summary>
    public static TrackRouteDecision Resolve(
        TrackMode requestedMode,
        string? intentText,
        string? additionalContext = null)
    {
        if (requestedMode is not TrackMode.Auto)
        {
            return new TrackRouteDecision
            {
                RequestedMode = requestedMode,
                EffectiveMode = requestedMode,
                Reason = "Explicit mode requested."
            };
        }

        var searchText = BuildSearchText(intentText, additionalContext);

        if (ContainsAny(searchText, s_enterpriseKeywords, out var enterpriseKeyword))
        {
            return new TrackRouteDecision
            {
                RequestedMode = requestedMode,
                EffectiveMode = TrackMode.Enterprise,
                Reason = $"Matched enterprise signal: '{enterpriseKeyword}'."
            };
        }

        if (ContainsAny(searchText, s_reasoningKeywords, out var reasoningKeyword))
        {
            return new TrackRouteDecision
            {
                RequestedMode = requestedMode,
                EffectiveMode = TrackMode.Reasoning,
                Reason = $"Matched reasoning signal: '{reasoningKeyword}'."
            };
        }

        if (ContainsAny(searchText, s_creativeKeywords, out var creativeKeyword))
        {
            return new TrackRouteDecision
            {
                RequestedMode = requestedMode,
                EffectiveMode = TrackMode.Creative,
                Reason = $"Matched creative signal: '{creativeKeyword}'."
            };
        }

        return new TrackRouteDecision
        {
            RequestedMode = requestedMode,
            EffectiveMode = TrackMode.Auto,
            Reason = "No strong routing signal detected."
        };
    }

    /// <summary>
    ///     Returns workflow name candidates for a mode, in lookup priority order.
    /// </summary>
    public static IReadOnlyList<string> GetWorkflowCandidates(string workflowName, TrackMode mode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowName);

        if (mode is TrackMode.Auto)
        {
            return [workflowName];
        }

        var modeName = ToWireValue(mode);
        var candidates = new List<string>(5)
        {
            $"{workflowName}.{modeName}",
            $"{workflowName}-{modeName}",
            $"{modeName}.{workflowName}",
            $"{modeName}-{workflowName}",
            workflowName
        };

        return [.. candidates.Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>
    ///     Returns mode-specific system guidance used by chat/workflow pipelines.
    /// </summary>
    public static string? BuildModeSystemPrompt(TrackMode mode) => mode switch
    {
        TrackMode.Creative =>
            "Creative mode: produce multiple distinct ideas, emphasize novelty, and return concise artifact-ready output.",
        TrackMode.Reasoning =>
            "Reasoning mode: structure analysis as hypotheses, evidence, and verification steps before conclusions.",
        TrackMode.Enterprise =>
            "Enterprise mode: apply policy/compliance constraints, call out risk and required approvals, and keep output audit-friendly.",
        _ => null
    };

    /// <summary>
    ///     Merges mode guidance into an optional caller-provided system prompt.
    /// </summary>
    public static string? MergeSystemPrompt(string? systemPrompt, TrackMode mode)
    {
        var modePrompt = BuildModeSystemPrompt(mode);
        if (string.IsNullOrWhiteSpace(modePrompt))
        {
            return string.IsNullOrWhiteSpace(systemPrompt) ? null : systemPrompt;
        }

        if (string.IsNullOrWhiteSpace(systemPrompt))
        {
            return modePrompt;
        }

        return $"{systemPrompt.Trim()}\n\n{modePrompt}";
    }

    /// <summary>
    ///     Builds short routing context text for telemetry-aware workflows.
    /// </summary>
    public static string? BuildRoutingContext(TrackRouteDecision decision)
    {
        if (decision.EffectiveMode is TrackMode.Auto)
        {
            return null;
        }

        var mode = ToWireValue(decision.EffectiveMode);
        return string.IsNullOrWhiteSpace(decision.Reason)
            ? $"Track mode: {mode}."
            : $"Track mode: {mode}. Router reason: {decision.Reason}";
    }

    /// <summary>
    ///     Returns a stable lowercase wire value.
    /// </summary>
    public static string ToWireValue(TrackMode mode) => mode.ToString().ToLowerInvariant();

    private static string BuildSearchText(string? intentText, string? additionalContext)
    {
        if (string.IsNullOrWhiteSpace(intentText) && string.IsNullOrWhiteSpace(additionalContext))
        {
            return string.Empty;
        }

        return $"{intentText ?? string.Empty}\n{additionalContext ?? string.Empty}";
    }

    private static bool ContainsAny(string text, string[] keywords, out string matchedKeyword)
    {
        foreach (var keyword in keywords)
        {
            if (text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                matchedKeyword = keyword;
                return true;
            }
        }

        matchedKeyword = string.Empty;
        return false;
    }
}
