using System.Text.Json;
using Qyl.AdGuard.Companion.Installation;
using Qyl.AdGuard.Companion.Messaging;
using Qyl.AdGuard.Companion.Telemetry;

namespace Qyl.AdGuard.Companion.Diagnostics;

internal sealed class NativeHostDoctor(string? callerOrigin, CompanionTelemetry telemetry)
{
    public static async Task<int> RunCliAsync(string[] args, CancellationToken cancellationToken)
    {
        var parameters = DoctorParams.FromArgs(args);
        await using var telemetry = await CompanionTelemetry.CreateAsync(Console.Error)
            .ConfigureAwait(false);
        var doctor = new NativeHostDoctor(callerOrigin: null, telemetry);
        var result = await doctor.InspectAsync(parameters, cancellationToken).ConfigureAwait(false);
        await Console.Error.WriteLineAsync(JsonSerializer.Serialize(
            result,
            CompanionJsonContext.Default.DoctorResult)).ConfigureAwait(false);
        return result.Checks.Any(static check => check.Status.Equals("fail", StringComparison.OrdinalIgnoreCase)) ? 1 : 0;
    }

    public async Task<DoctorResult> InspectAsync(DoctorParams parameters, CancellationToken cancellationToken)
    {
        var browser = parameters.Browser ?? "chrome";
        var extensionId = parameters.ExtensionId ?? ExtensionIdFromOrigin(callerOrigin);
        var manifestPath = NativeHostInstaller.GetManifestPath(browser);
        var manifestExists = File.Exists(manifestPath);
        NativeHostManifest? manifest = null;

        if (manifestExists)
        {
            await using var stream = File.OpenRead(manifestPath);
            manifest = await JsonSerializer.DeserializeAsync(
                stream,
                CompanionJsonContext.Default.NativeHostManifest,
                cancellationToken).ConfigureAwait(false);
        }

        var expectedOrigin = extensionId is null ? null : $"chrome-extension://{extensionId}/";
        var configuredPath = parameters.HostPath ?? manifest?.Path ?? Environment.ProcessPath;
        var checks = new List<DoctorCheck>
        {
            Check("manifestFile", manifestExists, manifestPath),
            Check("hostExecutable", configuredPath is not null && File.Exists(configuredPath), configuredPath ?? "unknown"),
            Check("nativeHostName", manifest?.Name == NativeHostConstants.Name, manifest?.Name ?? "manifest not installed"),
            Check("protocol", true, "stdio framing ready; stdout reserved for native messaging frames"),
            Check("qylTelemetry", telemetry.Enabled, telemetry.Enabled
                ? "OTEL_EXPORTER_OTLP_ENDPOINT configured"
                : "optional telemetry disabled")
        };

        if (expectedOrigin is not null)
        {
            var allowed = manifest?.AllowedOrigins?.Contains(expectedOrigin, StringComparer.Ordinal) is true;
            checks.Add(Check("extensionOrigin", allowed, expectedOrigin));
        }
        else
        {
            checks.Add(new DoctorCheck("extensionOrigin", "warn", "No extension id or caller origin available."));
        }

        return new DoctorResult(
            Browser: browser,
            ExtensionId: extensionId,
            CallerOrigin: callerOrigin,
            ManifestPath: manifestPath,
            HostPath: configuredPath,
            Checks: checks.ToArray());
    }

    private static DoctorCheck Check(string name, bool pass, string detail) =>
        new(name, pass ? "pass" : "fail", detail);

    private static string? ExtensionIdFromOrigin(string? origin)
    {
        if (origin is null || !origin.StartsWith("chrome-extension://", StringComparison.Ordinal))
            return null;

        var withoutScheme = origin["chrome-extension://".Length..];
        var slash = withoutScheme.IndexOf('/');
        return slash < 0 ? withoutScheme : withoutScheme[..slash];
    }
}

internal sealed class DoctorParams
{
    public string? Browser { get; init; }

    public string? ExtensionId { get; init; }

    public string? HostPath { get; init; }

    public static DoctorParams FromArgs(string[] args) => new()
    {
        Browser = ReadOption(args, "--browser"),
        ExtensionId = ReadOption(args, "--extension-id"),
        HostPath = ReadOption(args, "--host-path")
    };

    private static string? ReadOption(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }

        return null;
    }
}

internal sealed record DoctorResult(
    string Browser,
    string? ExtensionId,
    string? CallerOrigin,
    string ManifestPath,
    string? HostPath,
    DoctorCheck[] Checks);

internal sealed record DoctorCheck(string Name, string Status, string Detail);
