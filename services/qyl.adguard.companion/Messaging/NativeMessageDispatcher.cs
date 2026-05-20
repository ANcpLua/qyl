using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Qyl.AdGuard.Companion.Diagnostics;
using Qyl.AdGuard.Companion.Telemetry;

namespace Qyl.AdGuard.Companion.Messaging;

internal sealed class NativeMessageDispatcher(
    DnsDiagnostics dnsDiagnostics,
    NetworkBatchAnalyzer networkBatchAnalyzer,
    NativeHostDoctor doctor,
    CompanionTelemetry telemetry,
    CompanionStats stats)
{
    public async Task<NativeResponse> DispatchAsync(NativeRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Id))
            return NativeResponse.Fail(string.Empty, "invalid_request", "Request id is required.");

        if (request.SchemaVersion is not 1)
        {
            stats.RecordRequest(success: false);
            return NativeResponse.Fail(request.Id, "unsupported_schema", "Only schemaVersion 1 is supported.");
        }

        try
        {
            using var activity = telemetry.StartActivity($"native.{request.Method}");
            activity?.SetTag("qyl.adguard.method", request.Method);

            var result = request.Method switch
            {
                "hello" => ToElement(CreateHelloResult()),
                "dns.status" => ToElement(
                    await dnsDiagnostics.GetStatusAsync(cancellationToken).ConfigureAwait(false),
                    CompanionJsonContext.Default.DnsStatusResult),
                "page.snapshot" => ToElement(PageSnapshotAnalyzer.Analyze(
                    ReadParams(request.Params, CompanionJsonContext.Default.PageSnapshotParams))),
                "network.batch" => ToElement(networkBatchAnalyzer.Analyze(
                    ReadParams(request.Params, CompanionJsonContext.Default.NetworkBatchParams))),
                "rule.suggest" => ToElement(RuleSuggestionEngine.Suggest(
                    ReadParams(request.Params, CompanionJsonContext.Default.RuleSuggestParams))),
                "qyl.flush" => ToElement(telemetry.Flush()),
                "get_stats" => ToElement(stats.Snapshot(), CompanionJsonContext.Default.StatsSnapshot),
                "doctor" => ToElement(await doctor.InspectAsync(
                    ReadParams(request.Params, CompanionJsonContext.Default.DoctorParams),
                    cancellationToken).ConfigureAwait(false),
                    CompanionJsonContext.Default.DoctorResult),
                _ => throw new NotSupportedException($"Unknown method '{request.Method}'.")
            };

            stats.RecordRequest(success: true);
            return NativeResponse.Success(request.Id, result);
        }
        catch (NotSupportedException ex)
        {
            stats.RecordRequest(success: false);
            return NativeResponse.Fail(request.Id, "unknown_method", ex.Message);
        }
        catch (JsonException ex)
        {
            stats.RecordRequest(success: false);
            return NativeResponse.Fail(request.Id, "invalid_params", ex.Message);
        }
        catch (Exception ex)
        {
            stats.RecordRequest(success: false);
            return NativeResponse.Fail(request.Id, "companion_error", ex.Message);
        }
    }

    private HelloResult CreateHelloResult() => new(
        HostName: NativeHostConstants.Name,
        Version: typeof(NativeMessageDispatcher).Assembly.GetName().Version?.ToString() ?? "0.0.0",
        SchemaVersion: 1,
        Capabilities:
        [
            "dns.status",
            "page.snapshot",
            "network.batch",
            "rule.suggest",
            "qyl.flush",
            "get_stats",
            "doctor"
        ],
        QylTelemetryEnabled: telemetry.Enabled,
        InstallHint:
        "Publish the host, load extensions/qyl-adguard-companion unpacked, then run install --browser chrome --extension-id <id>.");

    private static T ReadParams<T>(JsonElement element, JsonTypeInfo<T> jsonTypeInfo)
    {
        if (element.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            return JsonSerializer.Deserialize("{}", jsonTypeInfo)
                   ?? throw new JsonException("Unable to construct empty parameter object.");

        return JsonSerializer.Deserialize(element.GetRawText(), jsonTypeInfo)
               ?? throw new JsonException("Parameter payload did not deserialize.");
    }

    private static JsonElement ToElement<T>(T value, JsonTypeInfo<T> jsonTypeInfo) =>
        JsonSerializer.SerializeToElement(value, jsonTypeInfo);

    private static JsonElement ToElement(HelloResult value) =>
        JsonSerializer.SerializeToElement(value, CompanionJsonContext.Default.HelloResult);

    private static JsonElement ToElement(PageSnapshotResult value) =>
        JsonSerializer.SerializeToElement(value, CompanionJsonContext.Default.PageSnapshotResult);

    private static JsonElement ToElement(NetworkBatchResult value) =>
        JsonSerializer.SerializeToElement(value, CompanionJsonContext.Default.NetworkBatchResult);

    private static JsonElement ToElement(RuleSuggestionResult value) =>
        JsonSerializer.SerializeToElement(value, CompanionJsonContext.Default.RuleSuggestionResult);

    private static JsonElement ToElement(QylFlushResult value) =>
        JsonSerializer.SerializeToElement(value, CompanionJsonContext.Default.QylFlushResult);
}
