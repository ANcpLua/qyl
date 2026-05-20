using System.Text.Json;
using System.Text.Json.Serialization;
using Qyl.AdGuard.Companion.Diagnostics;
using Qyl.AdGuard.Companion.Installation;
using Qyl.AdGuard.Companion.Telemetry;

namespace Qyl.AdGuard.Companion.Messaging;


internal sealed class NativeRequest
{
    public required string Id { get; init; }

    public int SchemaVersion { get; init; }

    public required string Method { get; init; }

    public JsonElement Params { get; init; }
}

internal sealed class NativeResponse
{
    public required string Id { get; init; }

    public bool Ok { get; init; }

    public JsonElement? Result { get; init; }

    public NativeError? Error { get; init; }

    public static NativeResponse Success(string id, JsonElement result) =>
        new() { Id = id, Ok = true, Result = result };

    public static NativeResponse Fail(string id, string code, string message) =>
        new() { Id = id, Ok = false, Error = new NativeError(code, message) };
}

internal sealed record NativeError(string Code, string Message);

internal sealed record HelloResult(
    string HostName,
    string Version,
    int SchemaVersion,
    string[] Capabilities,
    bool QylTelemetryEnabled,
    string InstallHint);

internal static class NativeHostConstants
{
    public const string Name = "dev.qyl.adguard_companion";

    public const string Description =
        "qyl AdGuard Native Messaging Companion for local diagnostics, rule assist, and optional qyl telemetry.";
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(NativeRequest))]
[JsonSerializable(typeof(NativeResponse))]
[JsonSerializable(typeof(NativeError))]
[JsonSerializable(typeof(HelloResult))]
[JsonSerializable(typeof(DnsStatusResult))]
[JsonSerializable(typeof(DnsResolver))]
[JsonSerializable(typeof(PageSnapshotParams))]
[JsonSerializable(typeof(PageSnapshotResult))]
[JsonSerializable(typeof(NetworkBatchParams))]
[JsonSerializable(typeof(NetworkEvent))]
[JsonSerializable(typeof(NetworkBatchResult))]
[JsonSerializable(typeof(NetworkHostSummary))]
[JsonSerializable(typeof(RuleSuggestParams))]
[JsonSerializable(typeof(RuleSuggestionResult))]
[JsonSerializable(typeof(RuleSuggestion))]
[JsonSerializable(typeof(QylFlushResult))]
[JsonSerializable(typeof(DoctorParams))]
[JsonSerializable(typeof(DoctorResult))]
[JsonSerializable(typeof(DoctorCheck))]
[JsonSerializable(typeof(NativeHostManifest))]
[JsonSerializable(typeof(StatsSnapshot))]
internal sealed partial class CompanionJsonContext : JsonSerializerContext;
