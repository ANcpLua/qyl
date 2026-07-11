using ANcpLua.Roslyn.Utilities;

namespace Qyl.Run;

/// <summary>
/// Configures where a collector's own telemetry (its self-instrumentation spans/logs) is exported.
/// The flow is strictly one-way: the owning collector exports OTLP to the target collector, and no
/// export path back to the owner can be composed — so there is no third collector and no feedback loop.
/// <code>
/// collector self-telemetry ──OTLP──&gt; diagnostics collector
/// diagnostics self-telemetry ──X──&gt; nowhere
/// </code>
/// Safety is automatic: self-reference and cycle validation always runs at composition time
/// (see <see cref="RejectSelfReference"/>), and the collector re-validates the resolved endpoint
/// against its own ports at startup. A dedicated target (<see cref="ExportToDedicatedCollector"/>)
/// additionally has its exporter force-disabled; an <see cref="ExportTo"/> target's exporter remains
/// the caller's responsibility apart from the cycle guard.
/// </summary>
public sealed class QylSelfTelemetryBuilder
{
    private readonly QylAppBuilder _app;
    private readonly IQylResourceBuilder _owner;
    private readonly string? _ownerProject;
    private IQylResourceBuilder? _target;

    internal QylSelfTelemetryBuilder(QylAppBuilder app, IQylResourceBuilder owner, string? ownerProject)
    {
        _app = app;
        _owner = owner;
        _ownerProject = ownerProject;
    }

    /// <summary>
    /// Export the owning collector's self-telemetry to an already-added collector resource.
    /// The export endpoint is the target's OTLP/HTTP receiver (its <c>QYL_OTLP_PORT</c>),
    /// never its API/dashboard port. The target's own exporter configuration is left to the
    /// caller; wiring the target back at the owner is rejected as a cycle.
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
    /// (<c>qyl.&lt;name&gt;.duckdb</c>), and its own service identity. Its telemetry pipeline is a
    /// guaranteed dead end: the exporter env is force-blanked (an inherited
    /// <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> cannot chain it onward) and its ingest runs Unsecured so
    /// the owner's exporter — which carries no auth headers — is never silently 401-dropped.
    /// </summary>
    public QylSelfTelemetryBuilder ExportToDedicatedCollector(string name, int? port = null)
    {
        Guard.NotNullOrWhiteSpace(name);
        if (_target is not null)
        {
            throw new InvalidOperationException(
                $"Self-telemetry for '{_owner.Resource.Name}' already has an export target ('{_target.Resource.Name}').");
        }

        // A dedicated diagnostics sink must also accept its owner's exporter, which sends no auth
        // headers: an inherited ApiKey mode would 401-drop every batch silently.
        _target = _app.AddCollector(name, port, _ownerProject)
            .WithIsolatedStorage()
            .DisableSelfTelemetryExport()
            .WithEnvironment(QylConstants.Env.QylOtlpAuthMode, QylConstants.Collector.UnsecuredAuthMode);

        return this;
    }

    /// <summary>
    /// Kept for call-site readability: self-reference and cycle validation is ALWAYS enforced by
    /// <c>Apply()</c> whether or not this is called — a resolved endpoint pointing back at any of
    /// the owning collector's own ports (api / otlp-http / grpc), or a target that already exports
    /// to the owner, fails composition unconditionally.
    /// </summary>
    public QylSelfTelemetryBuilder RejectSelfReference()
    {
        return this;
    }

    // Runs after the user callback returns: resolves the target's OTLP/HTTP endpoint, validates it
    // (always — safety is not opt-in), and injects the exporter configuration into the owning
    // collector's launch environment.
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

        if (target.OtlpHttpPort == owner.Port || target.OtlpHttpPort == owner.OtlpHttpPort ||
            target.OtlpHttpPort == owner.GrpcPort)
        {
            throw new InvalidOperationException(
                $"Self-telemetry endpoint for '{owner.Name}' resolves to port {target.OtlpHttpPort}, which is one of its own ports " +
                $"(api {owner.Port}, otlp-http {owner.OtlpHttpPort}, grpc {owner.GrpcPort}) — that would be a feedback loop.");
        }

        // Two-collector cycle guard: if the target already exports to the owner's OTLP receiver,
        // wiring owner→target would close a loop spanning two processes.
        if (target.Launch.Env.TryGetValue(QylConstants.Env.OtelExporterOtlpEndpoint, out var targetExport) &&
            !string.IsNullOrWhiteSpace(targetExport) &&
            Uri.TryCreate(targetExport, UriKind.Absolute, out var targetExportUri) &&
            (targetExportUri.Port == owner.OtlpHttpPort || targetExportUri.Port == owner.GrpcPort ||
             targetExportUri.Port == owner.Port))
        {
            throw new InvalidOperationException(
                $"Self-telemetry target '{target.Name}' already exports to '{owner.Name}' ({targetExport}); " +
                "wiring both directions would be a feedback loop.");
        }

        var endpoint = _target.GetEndpoint(QylConstants.EndpointKinds.OtlpHttp);

        _owner
            .WithEnvironment(QylConstants.Env.OtelExporterOtlpEndpoint, endpoint.ToString().TrimEnd('/'))
            // The .NET OTLP exporter defaults to grpc; the target address is the OTLP/HTTP receiver.
            .WithEnvironment(QylConstants.Env.OtelExporterOtlpProtocol, QylConstants.Collector.OtlpHttpProtobuf);
    }
}
