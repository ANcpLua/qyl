// src/qyl.contracts/Models/PagedResult.cs

namespace Qyl.Contracts.Models;

using System.Text.Json.Serialization;

/// <summary>
///     Cursor-paginated response wrapper for MCP tool consumption.
///     Collector endpoints return this shape; MCP tools deserialize it.
/// </summary>
public sealed record PagedResult<T>(
    [property: JsonPropertyName("items")] IReadOnlyList<T> Items,
    [property: JsonPropertyName("total_count")]
    int TotalCount,
    [property: JsonPropertyName("cursor")] string? Cursor);
