using System.Text.Json.Serialization;

namespace Qyl.Contracts.Intelligence;

/// <summary>Single step in an investigation strategy.</summary>
public sealed record InvestigationStep
{
    /// <summary>What to do (e.g. query_traces, get_code_location, compare_deployments)</summary>
    [JsonPropertyName("action")]
    public required string Action { get; init; }

    /// <summary>DuckDB query template or MCP tool name</summary>
    [JsonPropertyName("query")]
    public required string Query { get; init; }

    /// <summary>Human-readable explanation of this step</summary>
    [JsonPropertyName("description")]
    public required string Description { get; init; }
}
