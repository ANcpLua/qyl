namespace Qyl.Collector.Hosting;

// Second half of the two-phase self-export validation: the Qyl.Run composition layer rejects
// self-referential wiring when the graph is built, and this guard re-validates the RESOLVED
// environment at collector startup — catching hand-set or leaked endpoints the composition
// layer never saw. Exporting to our own OTLP receiver would re-ingest every ingest (a feedback
// amplifier), so a loopback endpoint on any of our own ports is fatal, not a warning.
internal static class CollectorSelfExportGuard
{
    private static readonly string[] s_loopbackHosts = ["localhost", "127.0.0.1", "::1", "[::1]", "0.0.0.0"];

    public static void ThrowIfSelfExporting(IConfiguration config, CollectorPortOptions ports)
    {
        var endpoint = config["OTEL_EXPORTER_OTLP_ENDPOINT"];
        if (string.IsNullOrWhiteSpace(endpoint)) return;
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)) return;

        if (!s_loopbackHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase)) return;

        if (uri.Port == ports.OtlpHttp || uri.Port == ports.Grpc || uri.Port == ports.Http)
        {
            throw new InvalidOperationException(
                $"OTEL_EXPORTER_OTLP_ENDPOINT ({endpoint}) points at this collector's own port " +
                $"(api {ports.Http}, otlp-http {ports.OtlpHttp}, grpc {ports.Grpc}). Exporting self-telemetry " +
                "into our own ingest pipeline would feedback-amplify; export to a separate diagnostics " +
                "collector instead (see Qyl.Run's ExportToDedicatedCollector).");
        }
    }
}
