// packages/Qyl.Contracts/Models/PagedResult.cs

using System.Text.Json.Serialization;

namespace Qyl.Contracts.Models;

/// <summary>
///     Cursor-paginated response wrapper for MCP tool consumption.
///     Collector endpoints return this shape; MCP tools deserialize it.
/// </summary>
public sealed record PagedResult<T>(
    [property: JsonPropertyName("items")] IReadOnlyList<T> Items,
    [property: JsonPropertyName("total_count")]
    int TotalCount,
    [property: JsonPropertyName("cursor")] string? Cursor);
