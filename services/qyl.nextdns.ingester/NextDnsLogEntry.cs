using System.Text.Json.Serialization;

namespace Qyl.NextDns.Ingester;

internal sealed record NextDnsLogPage(
    [property: JsonPropertyName("data")] List<NextDnsLogEntry>? Data,
    [property: JsonPropertyName("meta")] NextDnsLogMeta? Meta);

internal sealed record NextDnsLogMeta(
    [property: JsonPropertyName("pagination")]
    NextDnsPagination? Pagination);

internal sealed record NextDnsPagination(
    [property: JsonPropertyName("cursor")] string? Cursor);

/// <summary>
/// NextDNS log row. The public Logs API ships richer fields (encrypted, protocol,
/// device, reasons, …) but the ingester only commits to the small subset that the
/// shared <c>tracker_decisions</c> shape needs.
/// </summary>
internal sealed record NextDnsLogEntry(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("timestamp")] DateTimeOffset? Timestamp,
    [property: JsonPropertyName("domain")] string? Domain,
    [property: JsonPropertyName("root")] string? Root,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("reasons")] List<NextDnsReason>? Reasons,
    [property: JsonPropertyName("clientIp")] string? ClientIp,
    [property: JsonPropertyName("protocol")] string? Protocol);

internal sealed record NextDnsReason(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("name")] string? Name);

[JsonSerializable(typeof(NextDnsLogPage))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal sealed partial class IngesterJsonContext : JsonSerializerContext;
