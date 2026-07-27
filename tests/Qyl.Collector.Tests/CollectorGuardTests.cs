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

    [Theory]
    [InlineData("http://localhost:4318")]
    [InlineData("http://127.0.0.1:4317")]
    [InlineData("http://[::1]:5100")]
    public void SelfExport_to_an_own_ingest_port_throws_at_startup(string endpoint)
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => CollectorSelfExportGuard.ThrowIfSelfExporting(ConfigWith(endpoint), Ports));

        Assert.Contains("own port", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("http://localhost:9999")]
    [InlineData("https://otlp.example.com:4318")]
    public void SelfExport_guard_is_silent_for_absent_or_foreign_endpoints(string? endpoint)
    {
        CollectorSelfExportGuard.ThrowIfSelfExporting(ConfigWith(endpoint), Ports);
    }

    [Fact]
    public void Unwired_health_surface_fails_boot()
    {
        var builder = WebApplication.CreateSlimBuilder();
        var app = builder.Build();

        var exception = Assert.Throws<InvalidOperationException>(
            () => CollectorHealthGuard.ThrowIfHealthSurfaceUnwired(app));

        Assert.Contains(QylEndpoints.Health, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Health_surface_without_the_duckdb_check_fails_boot()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddHealthChecks()
            .AddCheck("self", static () => HealthCheckResult.Healthy());
        var app = builder.Build();
        app.MapGet(QylEndpoints.Health, static () => "ok");
        app.MapGet(QylEndpoints.Alive, static () => "ok");

        var exception = Assert.Throws<InvalidOperationException>(
            () => CollectorHealthGuard.ThrowIfHealthSurfaceUnwired(app));

        Assert.Contains("duckdb", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
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
