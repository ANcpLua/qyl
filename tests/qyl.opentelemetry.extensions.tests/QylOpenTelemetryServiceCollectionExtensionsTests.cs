using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Metrics;

namespace Qyl.OpenTelemetry.Extensions.Tests;

public sealed class QylOpenTelemetryServiceCollectionExtensionsTests
{
    private static readonly Uri s_traceEndpoint = new("http://localhost:4318/v1/traces");

    public static TheoryData<Action<QylOtelOptions>> HappyPathConfigurations() => new()
    {
        static o => { o.Endpoint = s_traceEndpoint; o.ServiceName = "orders-api"; o.MeterNames.Add("orders-api"); },
        static o => { o.EnableTracing = false; o.EnableMetrics = true; o.ServiceName = "orders-api"; },
        static o => { o.EnableTracing = false; o.ServiceName = "orders-api"; o.MeterNames.Add("orders-api"); },
        static o => { o.EnableTracing = false; o.ServiceName = "orders-api"; o.ConfigureMetrics = static m => m.AddMeter("orders-api"); },
    };

    [Theory]
    [MemberData(nameof(HappyPathConfigurations))]
    public void AddQylOpenTelemetry_RegistersServices_ForValidConfigurations(Action<QylOtelOptions> configure)
    {
        var services = new ServiceCollection();

        services.AddQylOpenTelemetry(configure);

        services.Should().NotBeEmpty();
    }

    [Fact]
    public void AddQylOpenTelemetry_InvokesConfigureMetricsCallback()
    {
        var services = new ServiceCollection();
        var configured = false;

        services.AddQylOpenTelemetry(o =>
        {
            o.EnableTracing = false;
            o.ServiceName = "orders-api";
            o.ConfigureMetrics = _ => configured = true;
        });

        configured.Should().BeTrue();
    }

    [Fact]
    public async Task AddQylOpenTelemetry_CollectsConfiguredMeter_ThroughOpenTelemetryReader()
    {
        var services = new ServiceCollection();
        var exporter = new CapturingMetricExporter();
        using var reader = new BaseExportingMetricReader(exporter);

        services.AddQylOpenTelemetry(o =>
        {
            o.EnableTracing = false;
            o.ServiceName = "orders-api";
            o.MeterNames.Add(" orders-api ");
            o.MeterNames.Add("orders-api");
            o.ConfigureMetrics = m => m.AddReader(reader);
        });

        await using var provider = services.BuildServiceProvider();
        var hosted = provider.GetServices<IHostedService>().ToList();
        foreach (var h in hosted) await h.StartAsync(TestContext.Current.CancellationToken);

        try
        {
            using var meter = new Meter("orders-api");
            meter.CreateCounter<long>("orders.processed", "{order}", "Processed orders.").Add(7);

            reader.Collect(timeoutMilliseconds: 10_000).Should().BeTrue();

            var metric = exporter.Metrics.Should().ContainSingle(m => m.Name == "orders.processed").Subject;
            metric.MeterName.Should().Be("orders-api");
            metric.Unit.Should().Be("{order}");
            metric.Description.Should().Be("Processed orders.");
            metric.Value.Should().Be(7);
        }
        finally
        {
            for (var i = hosted.Count - 1; i >= 0; i--)
                await hosted[i].StopAsync(TestContext.Current.CancellationToken);
        }
    }

    public static TheoryData<Action<QylOtelOptions>, string> RejectionCases() => new()
    {
        // Tracing on but no endpoint → Endpoint required
        { static o => { o.ServiceName = "orders-api"; }, nameof(QylOtelOptions.Endpoint) },
        // Missing service name variants
        { static o => { o.EnableTracing = false; o.EnableMetrics = true; o.ServiceName = null; }, nameof(QylOtelOptions.ServiceName) },
        { static o => { o.EnableTracing = false; o.EnableMetrics = true; o.ServiceName = ""; }, nameof(QylOtelOptions.ServiceName) },
        { static o => { o.EnableTracing = false; o.EnableMetrics = true; o.ServiceName = " "; }, nameof(QylOtelOptions.ServiceName) },
        // Invalid sample rates
        { static o => { o.EnableTracing = false; o.EnableMetrics = true; o.ServiceName = "orders-api"; o.SampleRate = -0.01; }, nameof(QylOtelOptions.SampleRate) },
        { static o => { o.EnableTracing = false; o.EnableMetrics = true; o.ServiceName = "orders-api"; o.SampleRate = 1.01; }, nameof(QylOtelOptions.SampleRate) },
        { static o => { o.EnableTracing = false; o.EnableMetrics = true; o.ServiceName = "orders-api"; o.SampleRate = double.NaN; }, nameof(QylOtelOptions.SampleRate) },
        { static o => { o.EnableTracing = false; o.EnableMetrics = true; o.ServiceName = "orders-api"; o.SampleRate = double.PositiveInfinity; }, nameof(QylOtelOptions.SampleRate) },
        // Whitespace meter name
        { static o => { o.Endpoint = s_traceEndpoint; o.ServiceName = "orders-api"; o.MeterNames.Add(" "); }, nameof(QylOtelOptions.MeterNames) },
    };

    [Theory]
    [MemberData(nameof(RejectionCases))]
    public void AddQylOpenTelemetry_RejectsInvalidConfiguration(Action<QylOtelOptions> configure, string expectedFieldName)
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddQylOpenTelemetry(configure));

        ex.Message.Should().Contain(expectedFieldName);
    }

    private sealed class CapturingMetricExporter : BaseExporter<Metric>
    {
        private readonly List<CapturedMetric> _metrics = [];

        public IReadOnlyList<CapturedMetric> Metrics => _metrics;

        public override ExportResult Export(in Batch<Metric> batch)
        {
            foreach (var metric in batch)
                foreach (var point in metric.GetMetricPoints())
                    _metrics.Add(new CapturedMetric(metric.MeterName, metric.Name, metric.Unit, metric.Description, point.GetSumLong()));
            return ExportResult.Success;
        }
    }

    private sealed record CapturedMetric(string MeterName, string Name, string Unit, string Description, long Value);
}
