using ANcpLua.Roslyn.Utilities;

namespace Qyl.Run;

/// <summary>
/// Composition-scope primitives on <see cref="IQylResourceBuilder"/>. These are the building
/// blocks <see cref="QylSelfTelemetryBuilder.ExportToDedicatedCollector"/> composes from; they
/// are public so a composition root can wire custom topologies without bypassing the model.
/// </summary>
public static class QylResourceBuilderExtensions
{
    /// <summary>
    /// Holds this resource's launch until every dependency has reported Ready. A dependency that
    /// terminally fails fails this resource too. Unknown names and cycles fail <c>Build()</c>.
    /// </summary>
    public static IQylResourceBuilder WaitFor(this IQylResourceBuilder builder,
        params IQylResourceBuilder[] dependencies)
    {
        Guard.NotNull(builder);
        Guard.NotNull(dependencies);

        var names = dependencies.Select(static d => d.Resource.Name).ToArray();
        if (names.Contains(builder.Resource.Name, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"Resource '{builder.Resource.Name}' cannot wait for itself.");
        }

        return builder.Update(r => r with
        {
            WaitsFor = [.. r.WaitsFor.Concat(names).Distinct(StringComparer.Ordinal)]
        });
    }

    /// <summary>Sets (or overrides) one environment variable in the resource's launch spec.</summary>
    public static IQylResourceBuilder WithEnvironment(this IQylResourceBuilder builder, string name, string value)
    {
        Guard.NotNull(builder);
        Guard.NotNullOrWhiteSpace(name);
        return builder.Update(r => r with
        {
            Launch = r.Launch with
            {
                Env = new Dictionary<string, string>(r.Launch.Env, StringComparer.Ordinal)
                {
                    [name] = value
                }
            }
        });
    }

    /// <summary>
    /// Gives a collector resource its own DuckDB file (<c>qyl.&lt;name&gt;.duckdb</c>) so two
    /// instances of the same collector project never contend for one storage file.
    /// </summary>
    public static IQylResourceBuilder WithIsolatedStorage(this IQylResourceBuilder builder)
    {
        Guard.NotNull(builder);
        return builder.WithEnvironment(QylConstants.Env.QylDataPath,
            string.Format(CultureInfo.InvariantCulture, QylConstants.Collector.DataPathTemplate,
                builder.Resource.Name));
    }

    /// <summary>
    /// Force-disables the resource's own OTLP exporter by blanking
    /// <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> — a blank endpoint reads as "no exporter" in the qyl
    /// service defaults and overrides any endpoint the runner's own environment would otherwise
    /// leak into the child. This is what makes a diagnostics sink a dead end.
    /// </summary>
    public static IQylResourceBuilder DisableSelfTelemetryExport(this IQylResourceBuilder builder)
    {
        Guard.NotNull(builder);
        return builder.WithEnvironment(QylConstants.Env.OtelExporterOtlpEndpoint, string.Empty);
    }

    /// <summary>
    /// Resolves one of the resource's declared endpoints: <c>"api"</c>, <c>"otlp-http"</c>, or
    /// <c>"otlp-grpc"</c>. Always the loopback address the runner supervises — an OTLP export
    /// target is the <c>otlp-http</c> receiver, never the API/dashboard port.
    /// </summary>
    public static Uri GetEndpoint(this IQylResourceBuilder builder, string kind)
    {
        Guard.NotNull(builder);
        var resource = builder.Resource;
        var port = kind switch
        {
            QylConstants.EndpointKinds.Api => resource.Port,
            QylConstants.EndpointKinds.OtlpHttp => resource.OtlpHttpPort,
            QylConstants.EndpointKinds.OtlpGrpc => resource.GrpcPort,
            _ => throw new ArgumentException(
                $"Unknown endpoint kind '{kind}'; expected '{QylConstants.EndpointKinds.Api}', " +
                $"'{QylConstants.EndpointKinds.OtlpHttp}' or '{QylConstants.EndpointKinds.OtlpGrpc}'.", nameof(kind))
        };

        if (port <= 0)
        {
            throw new InvalidOperationException(
                $"Resource '{resource.Name}' does not expose a '{kind}' endpoint.");
        }

        return new Uri(string.Format(CultureInfo.InvariantCulture, QylConstants.Network.LocalhostUrlTemplate,
            QylConstants.Network.Loopback, port));
    }
}
