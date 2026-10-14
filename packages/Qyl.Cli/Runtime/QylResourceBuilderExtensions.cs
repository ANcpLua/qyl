namespace Qyl.Cli.Runtime;

internal static class QylResourceBuilderExtensions
{
    internal static QylResourceBuilder WaitFor(this QylResourceBuilder builder,
        params QylResourceBuilder[] dependencies)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(dependencies);

        var names = dependencies.Select(static d => d.Name).ToArray();
        if (names.Contains(builder.Name, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"Resource '{builder.Name}' cannot wait for itself.");
        }

        return builder.Update(r => r with
        {
            WaitsFor = [.. r.WaitsFor.Concat(names).Distinct(StringComparer.Ordinal)]
        });
    }

    internal static QylResourceBuilder WithEnvironment(this QylResourceBuilder builder, string name, string value)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
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
    /// Gives a collector resource its own DuckDB file (<c>~/.qyl/qyl.&lt;name&gt;.duckdb</c>) so two
    /// instances of the same collector project never contend for one storage file, and so the
    /// files never land in the operator's working directory. A later
    /// <see cref="WithEnvironment"/> call for <c>QYL_DATA_PATH</c> overrides this default.
    /// </summary>
    internal static QylResourceBuilder WithIsolatedStorage(this QylResourceBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var dataHome = QylConstants.Collector.DefaultDataHome;
        Directory.CreateDirectory(dataHome);
        return builder.WithEnvironment(QylConstants.Env.QylDataPath,
            Path.Combine(dataHome, string.Format(CultureInfo.InvariantCulture,
                QylConstants.Collector.DataPathTemplate, builder.Name)));
    }

    /// <summary>
    /// Force-disables the resource's own OTLP exporter by blanking
    /// <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> — a blank endpoint reads as "no exporter" in the qyl
    /// service defaults and overrides any endpoint the runner's own environment would otherwise
    /// leak into the child. This is what makes a diagnostics sink a dead end.
    /// </summary>
    internal static QylResourceBuilder DisableSelfTelemetryExport(this QylResourceBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithEnvironment(QylConstants.Env.OtelExporterOtlpEndpoint, string.Empty);
    }

    internal static Uri GetOtlpHttpEndpoint(this QylResourceBuilder builder) =>
        GetEndpoint(builder, static resource => resource.OtlpHttpPort, "otlp-http");

    internal static QylResource GetResource(this QylResourceBuilder builder) => builder.Resource;

    private static Uri GetEndpoint(
        QylResourceBuilder builder,
        Func<QylResource, int> selectPort,
        string endpointName)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var resource = builder.Resource;
        var port = selectPort(resource);

        if (port <= 0)
        {
            throw new InvalidOperationException(
                $"Resource '{resource.Name}' does not expose a '{endpointName}' endpoint.");
        }

        return new Uri(string.Format(CultureInfo.InvariantCulture, QylConstants.Network.LocalhostUrlTemplate,
            QylConstants.Network.Loopback, port));
    }

}
