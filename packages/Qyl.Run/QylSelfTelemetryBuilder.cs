using ANcpLua.Roslyn.Utilities;
using Qyl.Run.Internal;

namespace Qyl.Run;

/// <summary>
/// Configures where a collector's own telemetry (its self-instrumentation spans/logs) is exported.
/// The flow is strictly one-way: the owning collector exports OTLP to the target collector, and the
/// target's own exporter stays disabled — so there is no third collector and no feedback loop.
/// <code>
/// collector self-telemetry ──OTLP──&gt; diagnostics collector
/// diagnostics self-telemetry ──X──&gt; nowhere
/// </code>
/// </summary>
public sealed class QylSelfTelemetryBuilder
{
    private readonly QylAppBuilder _app;
    private readonly IQylResourceBuilder _owner;
    private readonly string? _ownerProject;
    private IQylResourceBuilder? _target;
    private bool _rejectSelfReference;

    internal QylSelfTelemetryBuilder(QylAppBuilder app, IQylResourceBuilder owner, string? ownerProject)
    {
        _app = app;
        _owner = owner;
        _ownerProject = ownerProject;
    }

    /// <summary>
    /// Export the owning collector's self-telemetry to an already-added collector resource.
    /// The export endpoint is the target's OTLP/HTTP receiver (its <c>QYL_OTLP_PORT</c>),
    /// never its API/dashboard port.
    /// </summary>
    public QylSelfTelemetryBuilder ExportTo(IQylResourceBuilder collector)
    {
        Guard.NotNull(collector);
        if (_target is not null)
        {
            throw new InvalidOperationException(
                $"Self-telemetry for '{_owner.Resource.Name}' already has an export target ('{_target.Resource.Name}').");
        }

        _target = collector;
        return this;
    }

    /// <summary>
    /// Provision a dedicated diagnostics collector — the same collector project started as a second
    /// process/resource — and export the owning collector's self-telemetry to it. The dedicated
    /// instance gets its own API port, freshly claimed OTLP receiver ports, separate DuckDB storage
    /// (<c>qyl.&lt;name&gt;.duckdb</c>), and its own service identity; its exporter is force-disabled
    /// (inherited <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> is blanked) so its telemetry goes nowhere.
    /// </summary>
    public QylSelfTelemetryBuilder ExportToDedicatedCollector(string name, int? port = null)
    {
        Guard.NotNullOrWhiteSpace(name);
        if (_target is not null)
        {
            throw new InvalidOperationException(
                $"Self-telemetry for '{_owner.Resource.Name}' already has an export target ('{_target.Resource.Name}').");
        }

        // Fresh OTLP receiver ports: the primary already occupies the collector defaults (4318/4317),
        // so the dedicated instance must never fall back to them.
        var otlpHttpPort = PortAllocator.ClaimFreePort(QylConstants.Network.Loopback);
        var grpcPort = PortAllocator.ClaimFreePort(QylConstants.Network.Loopback);

        var dedicated = _app.AddCollector(name, port, _ownerProject);
        dedicated.Update(r => r with
        {
            OtlpHttpPort = otlpHttpPort,
            GrpcPort = grpcPort,
            Launch = r.Launch with
            {
                Env = Merge(r.Launch.Env, new Dictionary<string, string>
                {
                    [QylConstants.Env.QylOtlpPort] = otlpHttpPort.ToString(CultureInfo.InvariantCulture),
                    [QylConstants.Env.QylGrpcPort] = grpcPort.ToString(CultureInfo.InvariantCulture),
                    [QylConstants.Env.QylDataPath] =
                        string.Format(CultureInfo.InvariantCulture, QylConstants.Collector.DataPathTemplate, name),
                    // The loop-breaker: a blank endpoint reads as "no exporter" in the collector's
                    // service defaults, and explicitly overrides any OTEL_EXPORTER_OTLP_ENDPOINT the
                    // runner's own environment would otherwise leak into the child. Without this, an
                    // ambient endpoint could point the diagnostics instance back at the primary.
                    [QylConstants.Env.OtelExporterOtlpEndpoint] = string.Empty
                })
            }
        });

        _target = dedicated;
        return this;
    }

    /// <summary>
    /// Reject, at composition time, any resolved export endpoint that points back at the owning
    /// collector itself — its API port or either of its own OTLP receiver ports.
    /// </summary>
    public QylSelfTelemetryBuilder RejectSelfReference()
    {
        _rejectSelfReference = true;
        return this;
    }

    // Runs after the user callback returns: resolves the target's OTLP/HTTP endpoint, validates it,
    // and injects the exporter configuration into the owning collector's launch environment.
    internal void Apply()
    {
        if (_target is null)
        {
            throw new InvalidOperationException(
                $"selfTelemetry for '{_owner.Resource.Name}' was configured without an export target; " +
                "call ExportTo(...) or ExportToDedicatedCollector(...).");
        }

        var target = _target.Resource;
        var owner = _owner.Resource;

        // Exporting a collector's telemetry into its own ingest pipeline re-ingests the ingest —
        // a feedback amplifier. Same-resource wiring is always nonsense, so it is always rejected.
        if (ReferenceEquals(_target, _owner) || string.Equals(target.Name, owner.Name, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Collector '{owner.Name}' cannot export its self-telemetry to itself.");
        }

        if (target.OtlpHttpPort <= 0)
        {
            throw new InvalidOperationException(
                $"Self-telemetry target '{target.Name}' has no OTLP receiver port; only collector resources can receive self-telemetry.");
        }

        if (_rejectSelfReference &&
            (target.OtlpHttpPort == owner.Port || target.OtlpHttpPort == owner.OtlpHttpPort ||
             target.OtlpHttpPort == owner.GrpcPort))
        {
            throw new InvalidOperationException(
                $"Self-telemetry endpoint for '{owner.Name}' resolves to port {target.OtlpHttpPort}, which is one of its own ports " +
                $"(api {owner.Port}, otlp-http {owner.OtlpHttpPort}, grpc {owner.GrpcPort}) — that would be a feedback loop.");
        }

        var endpoint = string.Format(CultureInfo.InvariantCulture, QylConstants.Network.LocalhostUrlTemplate,
            QylConstants.Network.Loopback, target.OtlpHttpPort);

        _owner.Update(r => r with
        {
            Launch = r.Launch with
            {
                Env = Merge(r.Launch.Env, new Dictionary<string, string>
                {
                    [QylConstants.Env.OtelExporterOtlpEndpoint] = endpoint,
                    // The .NET OTLP exporter defaults to grpc; the target address is the OTLP/HTTP receiver.
                    [QylConstants.Env.OtelExporterOtlpProtocol] = QylConstants.Collector.OtlpHttpProtobuf
                })
            }
        });
    }

    private static Dictionary<string, string> Merge(
        IReadOnlyDictionary<string, string> current, Dictionary<string, string> overrides)
    {
        var merged = new Dictionary<string, string>(current, StringComparer.Ordinal);
        foreach (var kv in overrides) merged[kv.Key] = kv.Value;
        return merged;
    }
}
