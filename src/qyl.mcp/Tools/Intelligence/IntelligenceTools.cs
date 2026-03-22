using System.ComponentModel;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;
using Qyl.Contracts.Intelligence;
using qyl.mcp.Errors;
using qyl.mcp.Formatting;

namespace qyl.mcp.Tools.Intelligence;

[McpServerToolType]
public sealed class IntelligenceTools(HttpClient client)
{
    [McpServerTool(
        Name = "qyl.list_diagnostic_patterns",
        Title = "List Diagnostic Patterns",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("List all available diagnostic patterns from the static registry. Returns pattern IDs, categories, hypotheses, and required signals.")]
    public Task<string> ListDiagnosticPatternsAsync(
        [Description("Filter by category (genai, error, performance, infrastructure)")]
        string? category = null)
    {
        IEnumerable<DiagnosticPattern> patterns = DiagnosticPatterns.All;

        if (category is not null
            && Enum.TryParse<PatternCategory>(category, ignoreCase: true, out var cat))
        {
            patterns = patterns.Where(p => p.Category == cat);
        }

        var items = patterns.Select(p => new
        {
            p.Id,
            category = p.Category.ToString().ToLowerInvariant(),
            p.Hypothesis,
            p.Confidence,
            signal_count = p.Signals.Count
        }).ToList();

        var response = new StructuredResponse
        {
            Facts = items,
            Actions =
            [
                new SuggestedAction
                {
                    Tool = "qyl.evaluate_patterns",
                    Description = "Evaluate these patterns against a specific trace or issue"
                }
            ]
        };

        return Task.FromResult(ResponseFormatter.FormatStructured(response));
    }

    [McpServerTool(
        Name = "qyl.evaluate_patterns",
        Title = "Evaluate Patterns",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description("Given a trace or issue ID, extract signals from telemetry and evaluate all diagnostic patterns. Returns matched patterns with confidence scores.")]
    public async Task<string> EvaluatePatternsAsync(
        [Description("Trace ID to evaluate")] string? traceId = null,
        [Description("Issue ID to evaluate")] string? issueId = null,
        CancellationToken ct = default)
    {
        if (traceId is null && issueId is null)
            throw new QylQueryException("Provide either traceId or issueId.");

        var url = traceId is not null
            ? $"/api/v1/intelligence/evaluate?traceId={Uri.EscapeDataString(traceId)}"
            : $"/api/v1/intelligence/evaluate?issueId={Uri.EscapeDataString(issueId!)}";

        var response = await client.GetAsync(url, ct).ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.NotFound)
            throw new QylNotFoundException(traceId is not null ? "Trace" : "Issue");

        response.EnsureSuccessStatusCode();

        var result = await response.Content
            .ReadFromJsonAsync<IntelligenceEvaluateResponse>(
                IntelligenceJsonContext.Default.IntelligenceEvaluateResponse, ct)
            .ConfigureAwait(false);

        var structured = new StructuredResponse
        {
            Facts = result!.Matches,
            Evidence = new EvidenceInfo
            {
                Sources = [traceId ?? issueId!]
            },
            Actions = result.Matches.Count > 0
                ?
                [
                    new SuggestedAction
                    {
                        Tool = "qyl.explain_causal_chain",
                        Description = "Build causal graph from matched patterns"
                    },
                    new SuggestedAction
                    {
                        Tool = "qyl.suggest_investigation",
                        Description = "Get investigation strategy for the primary match"
                    }
                ]
                : null
        };

        return ResponseFormatter.FormatStructured(structured);
    }

    [McpServerTool(
        Name = "qyl.explain_causal_chain",
        Title = "Explain Causal Chain",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description("Given matched pattern IDs, build a causal graph identifying root causes and causal relationships between patterns.")]
    public async Task<string> ExplainCausalChainAsync(
        [Description("Comma-separated matched pattern IDs")]
        string patternIds,
        CancellationToken ct = default)
    {
        var url = $"/api/v1/intelligence/causal-chain?patternIds={Uri.EscapeDataString(patternIds)}";
        var response = await client.GetAsync(url, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var result = await response.Content
            .ReadFromJsonAsync<IntelligenceCausalResponse>(
                IntelligenceJsonContext.Default.IntelligenceCausalResponse, ct)
            .ConfigureAwait(false);

        var structured = new StructuredResponse
        {
            Facts = new
            {
                root_causes = result!.RootCauses,
                edges = result.Edges
            },
            Analysis = result.RootCauses.Count > 0
                ? new { summary = $"Identified {result.RootCauses.Count} root cause(s): {string.Join(", ", result.RootCauses)}" }
                : null,
            Actions =
            [
                new SuggestedAction
                {
                    Tool = "qyl.suggest_investigation",
                    Description = "Get investigation strategy for a root cause pattern"
                }
            ]
        };

        return ResponseFormatter.FormatStructured(structured);
    }

    [McpServerTool(
        Name = "qyl.suggest_investigation",
        Title = "Suggest Investigation",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description("Given a pattern ID, return the recommended investigation strategy with ordered steps.")]
    public async Task<string> SuggestInvestigationAsync(
        [Description("Pattern ID to investigate")]
        string patternId,
        CancellationToken ct = default)
    {
        var url = $"/api/v1/intelligence/strategy?patternId={Uri.EscapeDataString(patternId)}";
        var response = await client.GetAsync(url, ct).ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.NotFound)
            throw new QylNotFoundException("Strategy");

        response.EnsureSuccessStatusCode();

        var strategy = await response.Content
            .ReadFromJsonAsync<IntelligenceStrategyResponse>(
                IntelligenceJsonContext.Default.IntelligenceStrategyResponse, ct)
            .ConfigureAwait(false)
            ?? throw new QylNotFoundException("Strategy");

        var structured = new StructuredResponse
        {
            Facts = strategy,
            Actions = strategy.Steps.Count > 0
                ?
                [
                    new SuggestedAction
                    {
                        Tool = "qyl.execute_investigation_step",
                        Description = "Execute the first investigation step",
                        Parameters = new Dictionary<string, string>
                        {
                            ["strategyId"] = strategy.Id,
                            ["stepIndex"] = "0"
                        }
                    }
                ]
                : null
        };

        return ResponseFormatter.FormatStructured(structured);
    }

    [McpServerTool(
        Name = "qyl.execute_investigation_step",
        Title = "Execute Investigation Step",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description("Execute a single investigation strategy step (runs a DuckDB query against telemetry) and return structured results.")]
    public async Task<string> ExecuteInvestigationStepAsync(
        [Description("Strategy ID")] string strategyId,
        [Description("Step index (0-based)")] int stepIndex,
        [Description("Optional trace ID for context")]
        string? traceId = null,
        [Description("Optional service name for context")]
        string? service = null,
        CancellationToken ct = default)
    {
        var url = $"/api/v1/intelligence/execute-step?strategyId={Uri.EscapeDataString(strategyId)}&stepIndex={stepIndex}";
        if (traceId is not null)
            url += $"&traceId={Uri.EscapeDataString(traceId)}";
        if (service is not null)
            url += $"&service={Uri.EscapeDataString(service)}";

        var response = await client.GetAsync(url, ct).ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.NotFound)
            throw new QylNotFoundException("Strategy or step");

        response.EnsureSuccessStatusCode();

        var result = await response.Content
            .ReadFromJsonAsync<IntelligenceStepResponse>(
                IntelligenceJsonContext.Default.IntelligenceStepResponse, ct)
            .ConfigureAwait(false);

        var structured = new StructuredResponse
        {
            Facts = result!.QueryResults,
            Evidence = new EvidenceInfo
            {
                Sources = traceId is not null ? [traceId] : []
            },
            Actions = result.HasNextStep
                ?
                [
                    new SuggestedAction
                    {
                        Tool = "qyl.execute_investigation_step",
                        Description = "Execute the next step",
                        Parameters = new Dictionary<string, string>
                        {
                            ["strategyId"] = strategyId,
                            ["stepIndex"] = (stepIndex + 1).ToString()
                        }
                    }
                ]
                : null
        };

        return ResponseFormatter.FormatStructured(structured);
    }
}

// DTOs for collector intelligence API responses
internal sealed record IntelligenceEvaluateResponse(
    [property: JsonPropertyName("matches")]
    IReadOnlyList<PatternMatchDto> Matches);

internal sealed record PatternMatchDto(
    [property: JsonPropertyName("pattern_id")]
    string PatternId,
    [property: JsonPropertyName("category")]
    string Category,
    [property: JsonPropertyName("score")] double Score,
    [property: JsonPropertyName("hypothesis")]
    string Hypothesis,
    [property: JsonPropertyName("matched_signals")]
    IReadOnlyList<MatchedSignalDto> MatchedSignals);

internal sealed record MatchedSignalDto(
    [property: JsonPropertyName("attribute")]
    string Attribute,
    [property: JsonPropertyName("operator")]
    string Operator,
    [property: JsonPropertyName("expected")]
    string? Expected,
    [property: JsonPropertyName("observed")]
    string? Observed);

internal sealed record IntelligenceCausalResponse(
    [property: JsonPropertyName("root_causes")]
    IReadOnlyList<string> RootCauses,
    [property: JsonPropertyName("edges")]
    IReadOnlyList<CausalEdgeDto> Edges);

internal sealed record CausalEdgeDto(
    [property: JsonPropertyName("cause")] string Cause,
    [property: JsonPropertyName("effect")] string Effect,
    [property: JsonPropertyName("strength")]
    double Strength);

internal sealed record IntelligenceStrategyResponse(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("trigger_pattern")]
    string TriggerPattern,
    [property: JsonPropertyName("steps")]
    IReadOnlyList<InvestigationStepDto> Steps);

internal sealed record InvestigationStepDto(
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("query")] string Query,
    [property: JsonPropertyName("description")]
    string Description);

internal sealed record IntelligenceStepResponse(
    [property: JsonPropertyName("query_results")]
    object QueryResults,
    [property: JsonPropertyName("step_description")]
    string StepDescription,
    [property: JsonPropertyName("has_next_step")]
    bool HasNextStep);

[JsonSerializable(typeof(IntelligenceEvaluateResponse))]
[JsonSerializable(typeof(IntelligenceCausalResponse))]
[JsonSerializable(typeof(IntelligenceStrategyResponse))]
[JsonSerializable(typeof(IntelligenceStepResponse))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
internal sealed partial class IntelligenceJsonContext : JsonSerializerContext;
