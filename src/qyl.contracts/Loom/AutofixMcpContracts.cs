using System.Text.Json.Serialization;

namespace Qyl.Contracts.Loom;

/// <summary>
///     Request body for creating a new Loom autofix run.
/// </summary>
public sealed record LoomFixRunCreateRequest(
    [property: JsonPropertyName("policy")] string? Policy = null,
    [property: JsonPropertyName("instruction")] string? Instruction = null,
    [property: JsonPropertyName("stoppingPoint")] string? StoppingPoint = null);

/// <summary>Payload of a Collector autofix run object.</summary>
public sealed record LoomFixRunDto
{
    [JsonPropertyName("runId")] public required string RunId { get; init; }
    [JsonPropertyName("issueId")] public required string IssueId { get; init; }
    [JsonPropertyName("status")] public required string Status { get; init; }
    [JsonPropertyName("policy")] public required string Policy { get; init; }
    [JsonPropertyName("executionId")] public string? ExecutionId { get; init; }
    [JsonPropertyName("fixDescription")] public string? FixDescription { get; init; }
    [JsonPropertyName("confidenceScore")] public double? ConfidenceScore { get; init; }
    [JsonPropertyName("changesJson")] public string? ChangesJson { get; init; }
    [JsonPropertyName("instruction")] public string? Instruction { get; init; }
    [JsonPropertyName("stoppingPoint")] public string? StoppingPoint { get; init; }
    [JsonPropertyName("createdAt")] public DateTime? CreatedAt { get; init; }
    [JsonPropertyName("completedAt")] public DateTime? CompletedAt { get; init; }
}

/// <summary>List result returned by GET /api/v1/issues/{id}/fix-runs.</summary>
public sealed record LoomFixRunList
{
    [JsonPropertyName("items")] public required IReadOnlyList<LoomFixRunDto> Items { get; init; }
    [JsonPropertyName("total")] public required int Total { get; init; }
}

/// <summary>Autofix step payload returned by GET /api/v1/issues/{id}/fix-runs/{runId}/steps.</summary>
public sealed record LoomAutofixStepDto
{
    [JsonPropertyName("stepId")] public required string StepId { get; init; }
    [JsonPropertyName("runId")] public required string RunId { get; init; }
    [JsonPropertyName("stepNumber")] public required int StepNumber { get; init; }
    [JsonPropertyName("stepName")] public required string StepName { get; init; }
    [JsonPropertyName("status")] public required string Status { get; init; }
    [JsonPropertyName("inputJson")] public string? InputJson { get; init; }
    [JsonPropertyName("outputJson")] public string? OutputJson { get; init; }
    [JsonPropertyName("errorMessage")] public string? ErrorMessage { get; init; }
    [JsonPropertyName("startedAt")] public DateTime? StartedAt { get; init; }
    [JsonPropertyName("completedAt")] public DateTime? CompletedAt { get; init; }
    [JsonPropertyName("createdAt")] public DateTime? CreatedAt { get; init; }
}

/// <summary>List result for autofix step output.</summary>
public sealed record LoomAutofixStepList
{
    [JsonPropertyName("items")] public required IReadOnlyList<LoomAutofixStepDto> Items { get; init; }
    [JsonPropertyName("total")] public required int Total { get; init; }
}

/// <summary>Response body for approve/reject operations.</summary>
public sealed record LoomFixRunTransitionResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("runId")] string RunId);

/// <summary>Error shape returned by several collector endpoints.</summary>
public sealed record LoomErrorResponse(
    [property: JsonPropertyName("error")] string Error);

/// <summary>
///     Envelope used by mcp tools to return typed, non-string JSON for tool transport.
/// </summary>
public sealed record LoomToolEnvelope<T>
{
    [JsonPropertyName("success")] public required bool Success { get; init; }
    [JsonPropertyName("data")] [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public T? Data { get; init; }
    [JsonPropertyName("error")] [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Error { get; init; }
}

/// <summary>
///     Factory methods for <see cref="LoomToolEnvelope{T}" /> that avoid CA1000 (static members on generic types).
/// </summary>
public static class LoomToolEnvelope
{
    public static LoomToolEnvelope<T> Ok<T>(T data) => new() { Success = true, Data = data };
    public static LoomToolEnvelope<T> Fail<T>(string error) => new() { Success = false, Error = error };
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(LoomFixRunCreateRequest))]
[JsonSerializable(typeof(LoomFixRunDto))]
[JsonSerializable(typeof(LoomFixRunList))]
[JsonSerializable(typeof(LoomAutofixStepDto))]
[JsonSerializable(typeof(LoomAutofixStepList))]
[JsonSerializable(typeof(LoomFixRunTransitionResponse))]
[JsonSerializable(typeof(LoomErrorResponse))]
[JsonSerializable(typeof(LoomToolEnvelope<LoomFixRunDto>))]
[JsonSerializable(typeof(LoomToolEnvelope<LoomFixRunList>))]
[JsonSerializable(typeof(LoomToolEnvelope<LoomAutofixStepList>))]
[JsonSerializable(typeof(LoomToolEnvelope<LoomFixRunTransitionResponse>))]
public sealed partial class LoomMcpJsonContext : JsonSerializerContext;
