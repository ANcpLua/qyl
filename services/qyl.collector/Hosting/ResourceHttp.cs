using System.Text.Json.Serialization.Metadata;
using Microsoft.Net.Http.Headers;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Hosting;

/// <summary>
/// The shared write base (SCH-26) every user-resource route uses: JSON request bodies read into
/// generated contract models, <c>Idempotency-Key</c> on creating POSTs, <c>If-Match</c> on PATCH
/// and DELETE (428 when missing, 412 when stale), and the <c>ETag</c>/<c>Location</c> headers.
/// </summary>
internal static class ResourceHttp
{
    internal const string IdempotencyKeyHeader = "Idempotency-Key";
    internal const int MaxIdempotencyKeyLength = 255;

    /// <summary>A key older than this reads as absent and may be reused.</summary>
    internal static readonly TimeSpan IdempotencyKeyLifetime = TimeSpan.FromHours(24);

    internal static async Task<(T? Value, IResult? Error)> ReadJsonBodyAsync<T>(
        HttpRequest request,
        JsonTypeInfo<T> typeInfo,
        CancellationToken ct)
        where T : class
    {
        try
        {
            var value = await JsonSerializer.DeserializeAsync(request.Body, typeInfo, ct).ConfigureAwait(false);
            return value is null
                ? (null, InvalidBody("The request body must be a JSON object."))
                : (value, null);
        }
        // A missing discriminator or an unknown `kind`/`signal` surfaces as NotSupportedException.
        // The serializer's own message names internal types, so the response carries a fixed one.
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return (null, InvalidBody(
                "The request body is not valid JSON for this operation's contract model, " +
                "or a required member or discriminator is missing or unknown."));
        }
    }

    /// <summary>A body whose members are all optional may be omitted entirely.</summary>
    internal static async Task<(T? Value, IResult? Error)> ReadOptionalJsonBodyAsync<T>(
        HttpRequest request,
        JsonTypeInfo<T> typeInfo,
        Func<T> empty,
        CancellationToken ct)
        where T : class
    {
        if (request.ContentLength is 0 ||
            (request.ContentLength is null && !request.Headers.ContainsKey(HeaderNames.TransferEncoding)))
        {
            return (empty(), null);
        }

        return await ReadJsonBodyAsync(request, typeInfo, ct).ConfigureAwait(false);
    }

    internal static IResult InvalidBody(string message) =>
        ContractErrorResults.Validation("body", message, "body.invalid");

    internal static IResult? ReadIdempotencyKey(HttpRequest request, out string? key)
    {
        key = null;
        if (!request.Headers.TryGetValue(IdempotencyKeyHeader, out var values))
            return null;

        if (values.Count != 1 || string.IsNullOrEmpty(values[0]) || values[0]!.Length > MaxIdempotencyKeyLength)
        {
            return ContractErrorResults.Validation(
                IdempotencyKeyHeader,
                $"Idempotency-Key must be one value of 1 to {MaxIdempotencyKeyLength} characters.",
                "idempotency_key.invalid");
        }

        key = values[0];
        return null;
    }

    /// <summary>
    /// Reads <c>If-Match</c>. RFC 9110 strong comparison: <c>*</c> matches any current version,
    /// and a weak tag never matches. A bare, unquoted tag is read as the quoted tag it names, so a
    /// client that copied <c>etag</c> without its quotes still matches.
    /// </summary>
    internal static IResult? ReadIfMatch(HttpRequest request, out EntityTagCondition condition)
    {
        condition = default;
        var raw = string.Join(',', request.Headers.IfMatch.ToArray());
        if (string.IsNullOrWhiteSpace(raw))
            return ContractErrorResults.PreconditionRequired();

        var tags = new List<string>();
        foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (part is "*")
            {
                condition = new EntityTagCondition(true, []);
                return null;
            }

            tags.Add(part.StartsWith('"') || part.StartsWith("W/", StringComparison.Ordinal) ? part : $"\"{part}\"");
        }

        condition = new EntityTagCondition(false, tags);
        return null;
    }

    /// <summary>A new strong entity tag: 96 random bits, quoted as the ETag header requires.</summary>
    internal static string NewEtag() => $"\"{Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(12))}\"";

    /// <summary>A new opaque resource id; time-ordered so ids sort roughly by creation.</summary>
    internal static string NewId(string prefix) => prefix + Guid.CreateVersion7().ToString("N");

    /// <summary>SHA-256 of the canonical re-serialization, so whitespace or member order never changes it.</summary>
    internal static string Fingerprint<T>(T body, JsonTypeInfo<T> typeInfo) =>
        Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(body, typeInfo)));

    internal static IResult WithEtag<T>(HttpContext context, string etag, T body, JsonTypeInfo<T> typeInfo)
    {
        context.Response.Headers.ETag = etag;
        return Results.Json(body, typeInfo);
    }

    internal static IResult Created<T>(
        HttpContext context,
        string location,
        string etag,
        T body,
        JsonTypeInfo<T> typeInfo)
    {
        context.Response.Headers.ETag = etag;
        context.Response.Headers.Location = location;
        return Results.Json(body, typeInfo, statusCode: StatusCodes.Status201Created);
    }

    internal static IResult IdempotencyConflict(string key) =>
        ContractErrorResults.Conflict(
            key,
            "This Idempotency-Key was already used with a different request body.");

    internal static ulong NowUnixNano(TimeProvider timeProvider) =>
        QylTimeConversions.ToUnixNanoUnsigned(timeProvider.GetUtcNow());

    internal static ulong IdempotencyNotBefore(TimeProvider timeProvider) =>
        QylTimeConversions.ToUnixNanoUnsigned(timeProvider.GetUtcNow() - IdempotencyKeyLifetime);
}
