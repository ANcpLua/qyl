using System.Text.Json;
using Google.Protobuf;
using OpenTelemetry.Proto.Collector.Trace.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Resource.V1;
using OtlpSpan = OpenTelemetry.Proto.Trace.V1.Span;
using OtlpResourceSpans = OpenTelemetry.Proto.Trace.V1.ResourceSpans;
using OtlpScopeSpans = OpenTelemetry.Proto.Trace.V1.ScopeSpans;

namespace Qyl.Collector.Tests;

/// <summary>
/// An OTLP ScopeSpans carries three distinct identities: the scope's name, the scope's version and
/// the schema URL (the scope's, falling back to the resource's). Storage keeps each in its own
/// column; the API serves the scope as <c>instrumentation_scope</c> and the schema URLs through
/// <c>check_telemetry_health</c>. None of them may stand in for another, and an absent one is
/// absent, never an empty string.
/// </summary>
public sealed class InstrumentationScopeTests
{
    private const string ScopeName = "Qyl.Telemetry.AutoInstrumentation";
    private const string ScopeVersion = "3.1.0";
    private const string ResourceSchemaUrl = "https://opentelemetry.io/schemas/1.38.0";
    private const string ScopeSchemaUrl = "https://opentelemetry.io/schemas/1.39.0";

    private static readonly byte[] s_traceId = [.. Enumerable.Range(1, 16).Select(static i => (byte)i)];
    private static readonly byte[] s_spanId = [.. Enumerable.Range(1, 8).Select(static i => (byte)i)];

    private static ulong Now => (ulong)DateTimeOffset.UtcNow.AddMinutes(-2).ToUnixTimeMilliseconds() * 1_000_000;

    [Fact]
    public async Task Scope_name_version_and_schema_url_stay_distinct_from_ingest_to_the_api()
    {
        await using var host = await CollectorApiTestHost.StartAsync(TestContext.Current.CancellationToken);
        var request = TraceRequest(scope: new InstrumentationScope { Name = ScopeName, Version = ScopeVersion },
            resourceSchemaUrl: ResourceSchemaUrl, scopeSchemaUrl: ScopeSchemaUrl);
        await host.IngestTracesAsync(request);

        var stored = Assert.Single(await host.Store.GetSpansAsync("alpha", ct: TestContext.Current.CancellationToken));
        Assert.Equal(ScopeName, stored.ScopeName);
        Assert.Equal(ScopeVersion, stored.ScopeVersion);
        Assert.Equal(ScopeSchemaUrl, stored.SchemaUrl);

        var span = await SingleSpanAsync(host);
        var scope = span.GetProperty("instrumentation_scope");
        Assert.Equal(ScopeName, scope.GetProperty("name").GetString());
        Assert.Equal(ScopeVersion, scope.GetProperty("version").GetString());

        var health = await HealthAsync(host);
        Assert.Equal([ScopeSchemaUrl], health.GetProperty("schema_urls").EnumerateArray().Select(static url => url.GetString()));
    }

    [Fact]
    public async Task The_resource_schema_url_applies_when_the_scope_has_none()
    {
        await using var host = await CollectorApiTestHost.StartAsync(TestContext.Current.CancellationToken);
        await host.IngestTracesAsync(TraceRequest(scope: new InstrumentationScope { Name = ScopeName },
            resourceSchemaUrl: ResourceSchemaUrl, scopeSchemaUrl: ""));

        var stored = Assert.Single(await host.Store.GetSpansAsync("alpha", ct: TestContext.Current.CancellationToken));
        Assert.Equal(ScopeName, stored.ScopeName);
        Assert.Null(stored.ScopeVersion);
        Assert.Equal(ResourceSchemaUrl, stored.SchemaUrl);

        var scope = (await SingleSpanAsync(host)).GetProperty("instrumentation_scope");
        Assert.Equal(ScopeName, scope.GetProperty("name").GetString());
        Assert.False(scope.TryGetProperty("version", out _));
        Assert.Equal([ResourceSchemaUrl], (await HealthAsync(host)).GetProperty("schema_urls").EnumerateArray().Select(static url => url.GetString()));
    }

    [Fact]
    public async Task An_absent_scope_and_schema_url_are_absent_not_empty_strings()
    {
        await using var host = await CollectorApiTestHost.StartAsync(TestContext.Current.CancellationToken);
        await host.IngestTracesAsync(TraceRequest(scope: null, resourceSchemaUrl: "", scopeSchemaUrl: ""));

        var stored = Assert.Single(await host.Store.GetSpansAsync("alpha", ct: TestContext.Current.CancellationToken));
        Assert.Null(stored.ScopeName);
        Assert.Null(stored.ScopeVersion);
        Assert.Null(stored.SchemaUrl);

        var span = await SingleSpanAsync(host);
        Assert.False(span.TryGetProperty("instrumentation_scope", out _));
        Assert.Empty((await HealthAsync(host)).GetProperty("schema_urls").EnumerateArray());
    }

    /// <summary>
    /// The application scope carries the qyl registry's schema URL, so <c>check_telemetry_health</c> reports which
    /// registry release a producer compiled its starters against: the pin check made observable at runtime.
    /// </summary>
    [Fact]
    public async Task An_application_span_reports_the_qyl_registry_schema_url()
    {
        await using var host = await CollectorApiTestHost.StartAsync(TestContext.Current.CancellationToken);
        await host.IngestTracesAsync(TraceRequest(
            scope: new InstrumentationScope { Name = Spans.Source.Name, Version = Spans.Source.Version },
            resourceSchemaUrl: "", scopeSchemaUrl: Spans.Source.TelemetrySchemaUrl!));

        var stored = Assert.Single(await host.Store.GetSpansAsync("alpha", ct: TestContext.Current.CancellationToken));
        Assert.Equal("Qyl.Telemetry.Application", stored.ScopeName);
        Assert.Equal(["https://qyl.at/schemas/10.0.1"],
            (await HealthAsync(host)).GetProperty("schema_urls").EnumerateArray().Select(static url => url.GetString()));
    }

    private static ExportTraceServiceRequest TraceRequest(InstrumentationScope? scope, string resourceSchemaUrl, string scopeSchemaUrl)
    {
        var start = Now;
        var scopeSpans = new OtlpScopeSpans
        {
            SchemaUrl = scopeSchemaUrl,
            Spans =
            {
                new OtlpSpan
                {
                    TraceId = ByteString.CopyFrom(s_traceId),
                    SpanId = ByteString.CopyFrom(s_spanId),
                    Name = "GET /scope",
                    Kind = OtlpSpan.Types.SpanKind.Server,
                    StartTimeUnixNano = start,
                    EndTimeUnixNano = start + 1_000_000
                }
            }
        };
        if (scope is not null)
            scopeSpans.Scope = scope;

        return new ExportTraceServiceRequest
        {
            ResourceSpans =
            {
                new OtlpResourceSpans
                {
                    SchemaUrl = resourceSchemaUrl,
                    Resource = new Resource
                    {
                        Attributes =
                        {
                            new KeyValue { Key = "service.name", Value = new AnyValue { StringValue = "scope-service" } }
                        }
                    },
                    ScopeSpans = { scopeSpans }
                }
            }
        };
    }

    private static async Task<JsonElement> SingleSpanAsync(CollectorApiTestHost host)
    {
        var traceId = Convert.ToHexStringLower(s_traceId);
        using var response = await host.SendAsync(HttpMethod.Get, $"/api/v1/traces/{traceId}/spans", "reader");
        var body = await CollectorApiTestHost.JsonAsync(response);
        Assert.True(response.IsSuccessStatusCode, body.GetRawText());
        return Assert.Single(body.GetProperty("items").EnumerateArray());
    }

    private static async Task<JsonElement> HealthAsync(CollectorApiTestHost host)
    {
        using var response = await host.SendAsync(HttpMethod.Post, "/api/v1/analysis/check_telemetry_health", "reader", "{}");
        var body = await CollectorApiTestHost.JsonAsync(response);
        Assert.True(response.IsSuccessStatusCode, body.GetRawText());
        return body;
    }
}
