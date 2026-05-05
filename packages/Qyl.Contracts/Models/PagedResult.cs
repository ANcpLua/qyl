
using System.Text.Json.Serialization;

namespace Qyl.Contracts.Models;

public sealed record PagedResult<T>(
    [property: JsonPropertyName("items")] IReadOnlyList<T> Items,
    [property: JsonPropertyName("total_count")]
    int TotalCount,
    [property: JsonPropertyName("cursor")] string? Cursor);
