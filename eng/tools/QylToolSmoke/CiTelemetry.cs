using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace QylToolSmoke;

/// <summary>
/// CI dogfooding emitter: mirrors the smoke's phase markers as OTLP/JSON spans
/// so qyl.mcp's ci_log tool can answer "which leg hung on what" from qyl's own
/// telemetry. Convention (must match ci_log in qyl.mcp): resource service.name
/// "qyl-ci-smoke", session.id = one workflow run, one span per phase carrying a
/// ci.leg attribute, failures as span status error.
///
/// Entirely inert unless QYL_CI_OTLP_ENDPOINT is set, and a failed export never
/// fails the smoke — telemetry about CI must not become a new way to break CI.
/// </summary>
internal static class CiTelemetry
{
    private static readonly string? Endpoint =
        NonEmpty(Environment.GetEnvironmentVariable("QYL_CI_OTLP_ENDPOINT"));

    // The hosted collector guards /v1/traces with ApiKeyAuth (header x-otlp-api-key,
    // per the published OpenAPI); local collectors don't require it.
    private static readonly string? ApiKey =
        NonEmpty(Environment.GetEnvironmentVariable("QYL_CI_API_KEY"));

    private static readonly string RunId =
        NonEmpty(Environment.GetEnvironmentVariable("QYL_CI_RUN_ID")) ?? $"local-{Environment.MachineName}";

    private static readonly string Leg =
        NonEmpty(Environment.GetEnvironmentVariable("QYL_CI_LEG"))
        ?? System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier;

    private static readonly string TraceId = RandomHex(16);
    private static readonly List<(string Name, long StartNano, long EndNano, bool Ok, string? Message)> Phases = [];
    private static string? _openPhase;
    private static long _openPhaseStart;

    private static string? NonEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static long NowNano() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1_000_000;

    private static string RandomHex(int bytes) =>
        Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(bytes));

    /// <summary>Closes the previous phase as ok and opens the next one.</summary>
    public static void Mark(string message)
    {
        if (Endpoint is null) return;
        var now = NowNano();
        if (_openPhase is not null) Phases.Add((_openPhase, _openPhaseStart, now, true, null));
        _openPhase = message;
        _openPhaseStart = now;
    }

    /// <summary>Closes the open phase with the run's outcome and exports all spans.</summary>
    public static async Task FlushAsync(bool success, string? failure)
    {
        if (Endpoint is null) return;
        var now = NowNano();
        if (_openPhase is not null)
        {
            Phases.Add((_openPhase, _openPhaseStart, now, success, success ? null : failure));
            _openPhase = null;
        }
        if (Phases.Count == 0) return;

        var spans = Phases.Select(phase => new
        {
            traceId = TraceId,
            spanId = RandomHex(8),
            name = phase.Name,
            kind = 1,
            startTimeUnixNano = phase.StartNano.ToString(CultureInfo.InvariantCulture),
            endTimeUnixNano = phase.EndNano.ToString(CultureInfo.InvariantCulture),
            attributes = new[]
            {
                new { key = "ci.leg", value = new { stringValue = Leg } },
                new { key = "session.id", value = new { stringValue = RunId } }
            },
            status = phase.Ok
                ? new { code = 1, message = (string?)null }
                : new { code = 2, message = phase.Message }
        }).ToArray();

        // WhenWritingNull keeps ok-status spans free of "message": null — strict
        // OTLP/JSON parsers reject explicit nulls.
        var payload = JsonSerializer.Serialize(new
        {
            resourceSpans = new[]
            {
                new
                {
                    resource = new
                    {
                        attributes = new[]
                        {
                            new { key = "service.name", value = new { stringValue = "qyl-ci-smoke" } },
                            new { key = "session.id", value = new { stringValue = RunId } }
                        }
                    },
                    scopeSpans = new[] { new { scope = new { name = "qyl-ci-smoke" }, spans } }
                }
            }
        }, new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull });

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            if (ApiKey is not null) client.DefaultRequestHeaders.Add("x-otlp-api-key", ApiKey);
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            var target = $"{Endpoint.TrimEnd('/')}/v1/traces";
            using var response = await client.PostAsync(target, content);
            Console.WriteLine(response.IsSuccessStatusCode
                ? $"[ci-telemetry] exported {spans.Length} phase span(s) for {RunId}/{Leg}"
                : $"[ci-telemetry] export rejected with {(int)response.StatusCode} (ignored)");
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            Console.WriteLine($"[ci-telemetry] export failed: {exception.Message} (ignored)");
        }
    }
}
