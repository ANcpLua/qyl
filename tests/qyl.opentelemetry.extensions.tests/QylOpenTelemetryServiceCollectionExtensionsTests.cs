using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using Xunit;

namespace Qyl.OpenTelemetry.Extensions.Tests;

public sealed class QylOpenTelemetryServiceCollectionExtensionsTests
{
    private static readonly Uri s_traceEndpoint = new("http://localhost:4318/v1/traces");

    [Fact]
    public void AddQylOpenTelemetry_Allows_Metrics_Pipeline_With_Meter_Name_And_Callback()
    {
        var services = new ServiceCollection();
        var metricsConfigured = false;

        services.AddQylOpenTelemetry(o =>
        {
            o.Endpoint = s_traceEndpoint;
            o.ServiceName = "orders-api";
            o.MeterNames.Add("orders-api");
            o.ConfigureMetrics = _ => metricsConfigured = true;
        });

        Assert.True(metricsConfigured);
        Assert.NotEmpty(services);
    }

    [Fact]
    public void AddQylOpenTelemetry_Allows_Metrics_Only_Without_Trace_Endpoint()
    {
        var services = new ServiceCollection();

        services.AddQylOpenTelemetry(static o =>
        {
            o.EnableTracing = false;
            o.EnableMetrics = true;
            o.ServiceName = "orders-api";
        });

        Assert.NotEmpty(services);
    }

    [Fact]
    public void AddQylOpenTelemetry_Allows_MeterNames_Without_Trace_Endpoint()
    {
        var services = new ServiceCollection();

        services.AddQylOpenTelemetry(static o =>
        {
            o.EnableTracing = false;
            o.ServiceName = "orders-api";
            o.MeterNames.Add("orders-api");
        });

        Assert.NotEmpty(services);
    }

    [Fact]
    public void AddQylOpenTelemetry_Allows_ConfigureMetrics_Without_Qyl_Metric_Exporter_Endpoint()
    {
        var services = new ServiceCollection();
        var metricsConfigured = false;

        services.AddQylOpenTelemetry(o =>
        {
            o.EnableTracing = false;
            o.ServiceName = "orders-api";
            o.ConfigureMetrics = metrics =>
            {
                metricsConfigured = true;
                metrics.AddMeter("orders-api");
            };
        });

        Assert.True(metricsConfigured);
        Assert.NotEmpty(services);
    }

    [Fact]
    public async Task AddQylOpenTelemetry_Collects_Configured_Meter_Name_Through_OpenTelemetry_Reader()
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
            o.ConfigureMetrics = metrics => metrics.AddReader(reader);
        });

        await using var provider = services.BuildServiceProvider();
        List<IHostedService> hostedServices = [];
        foreach (var hostedService in provider.GetServices<IHostedService>())
        {
            hostedServices.Add(hostedService);
        }

        foreach (var hostedService in hostedServices)
            await hostedService.StartAsync(CancellationToken.None);

        try
        {
            using var meter = new Meter("orders-api");
            var counter = meter.CreateCounter<long>(
                name: "orders.processed",
                unit: "{order}",
                description: "Processed orders.");

            counter.Add(7);

            Assert.True(reader.Collect(timeoutMilliseconds: 10_000));

            var metric = Assert.Single(exporter.Metrics, static metric => metric.Name == "orders.processed");

            Assert.Equal("orders-api", metric.MeterName);
            Assert.Equal("{order}", metric.Unit);
            Assert.Equal("Processed orders.", metric.Description);
            Assert.Equal(7, metric.Value);
        }
        finally
        {
            for (var i = hostedServices.Count - 1; i >= 0; i--)
            {
                await hostedServices[i].StopAsync(CancellationToken.None);
            }
        }
    }

    [Fact]
    public void AddQylOpenTelemetry_Requires_Endpoint_When_Tracing_Is_Enabled()
    {
        var services = new ServiceCollection();

        var ex = Assert.Throws<InvalidOperationException>(() => services.AddQylOpenTelemetry(static o =>
        {
            o.ServiceName = "orders-api";
        }));

        Assert.Contains(nameof(QylOtelOptions.Endpoint), ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void AddQylOpenTelemetry_Rejects_Missing_Service_Name(string? serviceName)
    {
        var services = new ServiceCollection();

        var ex = Assert.Throws<InvalidOperationException>(() => services.AddQylOpenTelemetry(o =>
        {
            o.EnableTracing = false;
            o.EnableMetrics = true;
            o.ServiceName = serviceName;
        }));

        Assert.Contains(nameof(QylOtelOptions.ServiceName), ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void AddQylOpenTelemetry_Rejects_Invalid_Sample_Rate(double sampleRate)
    {
        var services = new ServiceCollection();

        var ex = Assert.Throws<InvalidOperationException>(() => services.AddQylOpenTelemetry(o =>
        {
            o.EnableTracing = false;
            o.EnableMetrics = true;
            o.ServiceName = "orders-api";
            o.SampleRate = sampleRate;
        }));

        Assert.Contains(nameof(QylOtelOptions.SampleRate), ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddQylOpenTelemetry_Rejects_Empty_Meter_Name_During_Registration()
    {
        var services = new ServiceCollection();

        var ex = Assert.Throws<InvalidOperationException>(() => services.AddQylOpenTelemetry(static o =>
        {
            o.Endpoint = s_traceEndpoint;
            o.ServiceName = "orders-api";
            o.MeterNames.Add(" ");
        }));

        Assert.Contains(nameof(QylOtelOptions.MeterNames), ex.Message, StringComparison.Ordinal);
    }

    private sealed class CapturingMetricExporter : BaseExporter<Metric>
    {
        private readonly List<CapturedMetric> _metrics = [];

        public IReadOnlyList<CapturedMetric> Metrics => _metrics;

        public override ExportResult Export(in Batch<Metric> batch)
        {
            foreach (var metric in batch)
            {
                foreach (var point in metric.GetMetricPoints())
                {
                    _metrics.Add(new CapturedMetric(
                        metric.MeterName,
                        metric.Name,
                        metric.Unit,
                        metric.Description,
                        point.GetSumLong()));
                }
            }

            return ExportResult.Success;
        }
    }

    private sealed record CapturedMetric(
        string MeterName,
        string Name,
        string Unit,
        string Description,
        long Value);
}
