namespace Qyl.Host;

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
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(dependencies);

        var owned = GetOwned(builder);
        var names = dependencies.Select(static d => d.Name).ToArray();
        if (names.Contains(builder.Name, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"Resource '{builder.Name}' cannot wait for itself.");
        }

        return owned.Update(r => r with
        {
            WaitsFor = [.. r.WaitsFor.Concat(names).Distinct(StringComparer.Ordinal)]
        });
    }

    /// <summary>Sets (or overrides) one environment variable in the resource's launch spec.</summary>
    public static IQylResourceBuilder WithEnvironment(this IQylResourceBuilder builder, string name, string value)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var owned = GetOwned(builder);
        if (owned.Resource.Launch is null)
        {
            throw new InvalidOperationException(
                $"Resource '{builder.Name}' is connection-only (no launch spec); there is no child " +
                "process environment to set.");
        }

        return owned.Update(r => r with
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
    internal static IQylResourceBuilder WithIsolatedStorage(this IQylResourceBuilder builder)
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
    internal static IQylResourceBuilder DisableSelfTelemetryExport(this IQylResourceBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithEnvironment(QylConstants.Env.OtelExporterOtlpEndpoint, string.Empty);
    }

    /// <summary>
    /// Configures this resource's standard OTLP/HTTP exporter to send to a collector resource and
    /// waits for that collector before launching the resource.
    /// </summary>
    public static IQylResourceBuilder WithOtlpExporter(
        this IQylResourceBuilder builder,
        IQylResourceBuilder collector)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(collector);
        return builder
            .WithEnvironment(QylConstants.Env.OtelExporterOtlpEndpoint,
                collector.GetOtlpHttpEndpoint().ToString().TrimEnd('/'))
            .WithEnvironment(QylConstants.Env.OtelExporterOtlpProtocol, QylConstants.Collector.OtlpHttpProtobuf)
            .WaitFor(collector);
    }

    public static Uri GetApiEndpoint(this IQylResourceBuilder builder) =>
        GetEndpoint(builder, static resource => resource.Port, "api");

    public static Uri GetOtlpHttpEndpoint(this IQylResourceBuilder builder) =>
        GetEndpoint(builder, static resource => resource.OtlpHttpPort, "otlp-http");

    public static Uri GetOtlpGrpcEndpoint(this IQylResourceBuilder builder) =>
        GetEndpoint(builder, static resource => resource.GrpcPort, "otlp-grpc");

    internal static QylResource GetResource(this IQylResourceBuilder builder) => GetOwned(builder).Resource;

    private static Uri GetEndpoint(
        IQylResourceBuilder builder,
        Func<QylResource, int> selectPort,
        string endpointName)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var resource = GetOwned(builder).Resource;
        var port = selectPort(resource);

        if (port <= 0)
        {
            throw new InvalidOperationException(
                $"Resource '{resource.Name}' does not expose a '{endpointName}' endpoint.");
        }

        return new Uri(string.Format(CultureInfo.InvariantCulture, QylConstants.Network.LocalhostUrlTemplate,
            QylConstants.Network.Loopback, port));
    }

    private static QylResourceBuilder GetOwned(IQylResourceBuilder builder) =>
        builder as QylResourceBuilder
        ?? throw new ArgumentException("The resource builder was not created by Qyl.Host.", nameof(builder));
}
