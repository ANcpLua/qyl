using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Qyl.Collector.Hosting;
using Qyl.Instrumentation;

namespace Qyl.Collector.Tests;

/// <summary>
/// G8: the fail-closed startup guards throw, asserted as startup tests rather than by
/// inspection. Self-export against any own ingest port is fatal; a foreign endpoint or
/// an absent endpoint is silent (the required-endpoint layer in the hosting package owns
/// "no endpoint means do not export"); an unwired health surface fails boot.
/// </summary>
[TestClass]
public sealed class CollectorGuardTests
{
    private static CollectorPortOptions Ports => new()
    {
        BindAddress = IPAddress.Loopback,
        Http = 5100,
        OtlpHttp = 4318,
        Grpc = 4317,
    };

    private static IConfiguration ConfigWith(string? endpoint)
    {
        var values = new Dictionary<string, string?>();
        if (endpoint is not null)
            values["OTEL_EXPORTER_OTLP_ENDPOINT"] = endpoint;
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    [TestMethod]
    [DataRow("http://localhost:4318")]
    [DataRow("http://127.0.0.1:4317")]
    [DataRow("http://[::1]:5100")]
    public void SelfExport_to_an_own_ingest_port_throws_at_startup(string endpoint)
    {
        var exception = Assert.ThrowsExactly<InvalidOperationException>(
            () => CollectorSelfExportGuard.ThrowIfSelfExporting(ConfigWith(endpoint), Ports));

        Assert.Contains("own port", exception.Message, StringComparison.Ordinal);
    }

    // Foreign endpoints are IP literals from TEST-NET-3 (RFC 5737): never routable, never
    // on a local interface, and — being literals — resolved without a DNS query, so the
    // assertion cannot depend on the resolver environment of the machine running it.
    [TestMethod]
    [DataRow(null)]
    [DataRow("http://203.0.113.9:9999")]
    [DataRow("http://203.0.113.9:4318")]
    public void SelfExport_guard_is_silent_for_absent_or_foreign_endpoints(string? endpoint)
    {
        CollectorSelfExportGuard.ThrowIfSelfExporting(ConfigWith(endpoint), Ports);
    }

    [TestMethod]
    public void Missing_health_routes_fail_boot_even_when_storage_checks_are_registered()
    {
        // The duckdb check is registered so only the route-mapping branch can throw:
        // asserting on a substring both branches emit would prove neither.
        var builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddHealthChecks()
            .AddCheck("duckdb", static () => HealthCheckResult.Healthy());
        var app = builder.Build();

        var exception = Assert.ThrowsExactly<InvalidOperationException>(
            () => CollectorHealthGuard.ThrowIfHealthSurfaceUnwired(app));

        Assert.Contains("booted without a mapped", exception.Message, StringComparison.Ordinal);
        Assert.Contains(QylEndpoints.Health, exception.Message, StringComparison.Ordinal);
    }

    [TestMethod]
    public void Health_surface_without_the_duckdb_check_fails_boot()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddHealthChecks()
            .AddCheck("self", static () => HealthCheckResult.Healthy());
        var app = builder.Build();
        app.MapGet(QylEndpoints.Health, static () => "ok");
        app.MapGet(QylEndpoints.Alive, static () => "ok");

        var exception = Assert.ThrowsExactly<InvalidOperationException>(
            () => CollectorHealthGuard.ThrowIfHealthSurfaceUnwired(app));

        Assert.Contains("duckdb", exception.Message, StringComparison.Ordinal);
    }

    [TestMethod]
    public void Fully_wired_health_surface_boots()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddHealthChecks()
            .AddCheck("duckdb", static () => HealthCheckResult.Healthy());
        var app = builder.Build();
        app.MapGet(QylEndpoints.Health, static () => "ok");
        app.MapGet(QylEndpoints.Alive, static () => "ok");

        CollectorHealthGuard.ThrowIfHealthSurfaceUnwired(app);
    }
}
