using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DuckDB.NET.Data;
using Google.Protobuf;
using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Collector.Metrics.V1;
using OpenTelemetry.Proto.Collector.Trace.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Logs.V1;
using OpenTelemetry.Proto.Metrics.V1;
using OtlpResource = OpenTelemetry.Proto.Resource.V1.Resource;
using OtlpSpan = OpenTelemetry.Proto.Trace.V1.Span;
using ResourceSpans = OpenTelemetry.Proto.Trace.V1.ResourceSpans;
using ScopeSpans = OpenTelemetry.Proto.Trace.V1.ScopeSpans;

namespace Qyl.Collector.Tests;

/// <summary>
/// Stored databases are never migrated for an encoding change, so the bytes the Collector persists
/// for attributes, log bodies and metric observations, and every identifier hashed from them, are
/// a format. This ingests every attribute value, log body and metric point kind, every identifying
/// metric property and untimed logs through real OTLP, then compares each persisted column, each
/// derived identifier and the API readback with Fixtures/storage-identity/golden.txt, which the
/// contract-serializing collector wrote.
/// </summary>
[TestClass]
public sealed class StorageIdentityGoldenTests(TestContext testContext)
{
    private const ulong Time = CollectorApiTestHost.FixtureUnixNano - 600_000_000_000;
    private const int RawValueLimit = 16 * 1024;
    private const int PrefixLength = 2048;
    private const string TraceHex = "101112131415161718191a1b1c1d1e1f";
    private const string ScopeSchema = "https://opentelemetry.io/schemas/1.40.0";

    private static readonly string[] s_metricNames =
    [
        "golden.gauge", "golden.sum.int", "golden.sum.double", "golden.histogram",
        "golden.exponential", "golden.summary", "golden.plain", "golden.identity"
    ];

    [TestMethod]
    public async Task Persisted_encodings_and_identifiers_match_the_golden_record()
    {
        var path = Path.Combine(testContext.TestTempDirectory!, "storage-identity.duckdb");
        var output = new StringBuilder();
        await using (var host = await CollectorApiTestHost.StartAsync(testContext.CancellationToken, path))
        {
            await IngestAsync(host, "/v1/metrics", Metrics(), static body =>
                ExportMetricsServiceResponse.Parser.ParseFrom(body).PartialSuccess);
            await IngestAsync(host, "/v1/logs", Logs(), static body =>
                ExportLogsServiceResponse.Parser.ParseFrom(body).PartialSuccess);
            await IngestAsync(host, "/v1/traces", Traces(), static body =>
                ExportTraceServiceResponse.Parser.ParseFrom(body).PartialSuccess);
            await ReadApiAsync(host, output);
        }

        // The database is read after the store closes, so the row values are the ones on disk.
        AppendDatabase(path, output);
        var actual = output.ToString();
        var golden = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "storage-identity", "golden.txt"),
            testContext.CancellationToken);
        var expected = string.Join('\n', golden.ReplaceLineEndings("\n").Split('\n')
            .Where(static line => !line.StartsWith('#'))).TrimEnd('\n') + "\n";
        if (actual == expected) return;

        var actualPath = Path.Combine(testContext.TestTempDirectory!, "storage-identity.actual.txt");
        await File.WriteAllTextAsync(actualPath, actual, testContext.CancellationToken);
        testContext.AddResultFile(actualPath);
        var expectedLines = expected.Split('\n');
        var actualLines = actual.Split('\n');
        var line = Enumerable.Range(0, Math.Max(expectedLines.Length, actualLines.Length)).First(index =>
            index >= expectedLines.Length || index >= actualLines.Length || expectedLines[index] != actualLines[index]);
        Assert.Fail($"""
            Persisted storage bytes differ from the golden record at line {line + 1}; the full output is {actualPath}.
            expected: {(line < expectedLines.Length ? expectedLines[line] : "<end>")}
            actual:   {(line < actualLines.Length ? actualLines[line] : "<end>")}
            """);
    }

    private async Task IngestAsync(CollectorApiTestHost host, string route, IMessage request,
        Func<byte[], IMessage?> partialSuccess)
    {
        using var response = await host.SendProtobufAsync(route, request, "ingest-reader");
        var body = await response.Content.ReadAsByteArrayAsync(testContext.CancellationToken);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, route);
        Assert.IsNull(partialSuccess(body), route);
    }

    private async Task ReadApiAsync(CollectorApiTestHost host, StringBuilder output)
    {
        foreach (var metric in s_metricNames)
        {
            var series = await GetAsync(host, output, $"/api/v1/metrics/{metric}/series?limit=1000");
            foreach (var item in series.GetProperty("items").EnumerateArray())
                await GetAsync(host, output,
                    $"/api/v1/metrics/{metric}/series/{item.GetProperty("series_id").GetString()}/points?limit=1000");
        }

        await GetAsync(host, output, "/api/v1/logs?limit=1000");
        await GetAsync(host, output, $"/api/v1/traces/{TraceHex}/spans?limit=1000");
    }

    private async Task<JsonElement> GetAsync(CollectorApiTestHost host, StringBuilder output, string route)
    {
        using var response = await host.SendAsync(HttpMethod.Get, route, CollectorApiTestHost.ReaderApiKey);
        var text = await response.Content.ReadAsStringAsync(testContext.CancellationToken);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, text);
        using var document = JsonDocument.Parse(text);
        output.Append("GET ").Append(route).Append('\n');
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (property is { Name: "items", Value.ValueKind: JsonValueKind.Array })
            {
                foreach (var item in property.Value.EnumerateArray())
                    output.Append("  item ").Append(Format(item.GetRawText())).Append('\n');
                continue;
            }

            output.Append("  ").Append(property.Name).Append(' ').Append(Format(property.Value.GetRawText())).Append('\n');
        }

        return document.RootElement.Clone();
    }

    private static void AppendDatabase(string path, StringBuilder output)
    {
        using var connection = new DuckDBConnection($"DataSource={path}");
        connection.Open();
        AppendRows(output, connection, "metric_series",
            "SELECT series_id, instrument_id, metric_name, metric_kind, temporality, is_monotonic, unit, service_name, " +
            "resource_schema_url, scope_name, scope_version, scope_schema_url, attributes_json, resource_json, " +
            "resource_entity_refs_json, scope_attributes_json FROM metric_series ORDER BY series_id");
        AppendRows(output, connection, "metric_observations",
            "SELECT series_id, time_unix_nano, observation_id, measurement_id, flags, data_json, exemplars_json, " +
            "instrument_metadata_json FROM metric_observations ORDER BY series_id, time_unix_nano, observation_id");
        AppendRows(output, connection, "metric_points",
            "SELECT series_id, time_unix_nano, value, histogram_count, histogram_sum, histogram_min, histogram_max, " +
            "bucket_bounds_json, bucket_counts_json, has_conflict, projection_unavailable_reason FROM metric_points " +
            "ORDER BY series_id, time_unix_nano");
        AppendRows(output, connection, "metric_exemplars",
            "SELECT series_id, time_unix_nano, trace_id FROM metric_exemplars ORDER BY series_id, time_unix_nano, trace_id");
        AppendRows(output, connection, "logs",
            "SELECT log_id, time_unix_nano, observed_time_unix_nano, event_name, body, attributes_json, resource_json, " +
            "resource_entity_refs_json, scope_attributes_json FROM logs ORDER BY log_id");
        AppendRows(output, connection, "spans",
            "SELECT span_id, attributes_json, resource_json, resource_entity_refs_json, scope_attributes_json, " +
            "events_json, links_json FROM spans ORDER BY trace_id, span_id");
    }

    private static void AppendRows(StringBuilder output, DuckDBConnection connection, string table, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            output.Append(table).Append(' ').Append(Format(Column(reader, 0))).Append('\n');
            for (var column = 1; column < reader.FieldCount; column++)
                output.Append("  ").Append(reader.GetName(column)).Append(' ').Append(Format(Column(reader, column))).Append('\n');
        }
    }

    private static string? Column(System.Data.Common.DbDataReader reader, int column) =>
        reader.IsDBNull(column) ? null : Convert.ToString(reader.GetValue(column), CultureInfo.InvariantCulture);

    // Values large enough to bury the readable lines are pinned by their exact UTF-8 digest and
    // shown by their opening characters.
    private static string Format(string? value)
    {
        if (value is null) return "(null)";
        var bytes = Encoding.UTF8.GetBytes(value);
        if (bytes.Length <= RawValueLimit) return value;
        var shown = char.IsHighSurrogate(value[PrefixLength - 1]) ? PrefixLength - 1 : PrefixLength;
        return $"sha256:{Convert.ToHexStringLower(SHA256.HashData(bytes))} ({bytes.Length} bytes) {value[..shown]}...";
    }

    private static ExportMetricsServiceRequest Metrics()
    {
        var gauge = new Metric { Name = "golden.gauge", Unit = "{item}", Description = "Golden gauge", Gauge = new Gauge() };
        gauge.Metadata.Add(EveryKind("golden.metadata"));
        var traced = new Exemplar { TimeUnixNano = Time - 1, AsInt = -7, TraceId = Id(16, 0x20), SpanId = Id(8, 0x30) };
        traced.FilteredAttributes.Add(EveryKind("golden.exemplar"));
        var untraced = new Exemplar { TimeUnixNano = Time - 2, AsDouble = 0.25 };
        untraced.FilteredAttributes.Add(Pair("golden.exemplar.only", Text("untraced")));
        var first = NumberPoint(0, static point => point.AsInt = long.MaxValue);
        first.Exemplars.Add([traced, untraced]);
        gauge.Gauge.DataPoints.Add(
        [
            first,
            NumberPoint(1, static point => point.AsDouble = 42),
            NumberPoint(2, static point => point.AsDouble = 9_223_372_036_854_775_808d),
            NumberPoint(3, static point => point.AsDouble = double.NaN),
            NumberPoint(4, static point => point.AsDouble = double.NegativeInfinity),
            NumberPoint(5, static _ => { }),
            NumberPoint(6, static point => { point.AsDouble = 0.1; point.Flags = 1; }),
            NumberPoint(7, static point => point.AsDouble = -0.0),
            NumberPoint(8, static point => point.AsDouble = 123_456_789.125),
            NumberPoint(9, static point => point.AsInt = long.MinValue)
        ]);

        var sumInt = new Metric
        {
            Name = "golden.sum.int", Unit = "By",
            Sum = new Sum { AggregationTemporality = AggregationTemporality.Cumulative, IsMonotonic = true }
        };
        var counted = new NumberDataPoint { StartTimeUnixNano = Time - 1_000, TimeUnixNano = Time, AsInt = 5 };
        counted.Attributes.Add(Pair("golden.sum", Text("int")));
        counted.Exemplars.Add(new Exemplar { TimeUnixNano = Time, AsInt = 3, TraceId = Id(16, 0x40), SpanId = Id(8, 0x50) });
        sumInt.Sum.DataPoints.Add(counted);

        var sumDouble = new Metric
        {
            Name = "golden.sum.double",
            Sum = new Sum { AggregationTemporality = AggregationTemporality.Delta, IsMonotonic = false }
        };
        var delta = new NumberDataPoint { StartTimeUnixNano = Time - 1_000, TimeUnixNano = Time, AsDouble = 1.5e-7 };
        delta.Attributes.Add(Pair("golden.sum", Text("double")));
        sumDouble.Sum.DataPoints.Add(delta);

        var histogram = new Metric
        {
            Name = "golden.histogram", Unit = "ms",
            Histogram = new Histogram { AggregationTemporality = AggregationTemporality.Cumulative }
        };
        var full = new HistogramDataPoint
        {
            StartTimeUnixNano = Time - 1_000, TimeUnixNano = Time, Count = 6, Sum = 12.5, Min = 0.5, Max = 7,
            ExplicitBounds = { 1, 2.5, 10 }, BucketCounts = { 1, 2, 3, 0 }
        };
        full.Attributes.Add(Pair("golden.histogram", Text("full")));
        full.Exemplars.Add(new Exemplar { TimeUnixNano = Time, AsDouble = 2.25, TraceId = Id(16, 0x60), SpanId = Id(8, 0x70) });
        var unrecorded = new HistogramDataPoint { StartTimeUnixNano = Time - 1_000, TimeUnixNano = Time + 1, Flags = 1 };
        unrecorded.Attributes.Add(Pair("golden.histogram", Text("full")));
        var empty = new HistogramDataPoint { TimeUnixNano = Time };
        empty.Attributes.Add(Pair("golden.histogram", Text("empty")));
        var aggregateOnly = new HistogramDataPoint { TimeUnixNano = Time, Count = 3, Sum = 4.5 };
        aggregateOnly.Attributes.Add(Pair("golden.histogram", Text("aggregate-only")));
        histogram.Histogram.DataPoints.Add([full, unrecorded, empty, aggregateOnly]);

        var exponential = new Metric
        {
            Name = "golden.exponential", Unit = "s",
            ExponentialHistogram = new ExponentialHistogram { AggregationTemporality = AggregationTemporality.Delta }
        };
        var native = new ExponentialHistogramDataPoint
        {
            StartTimeUnixNano = Time - 1_000, TimeUnixNano = Time, Count = 10, Sum = -3.5, Min = -2, Max = 4,
            Scale = 3, ZeroCount = 1, ZeroThreshold = 1e-9,
            Positive = new ExponentialHistogramDataPoint.Types.Buckets { Offset = -2, BucketCounts = { 1, 2, 3 } },
            Negative = new ExponentialHistogramDataPoint.Types.Buckets { Offset = 1, BucketCounts = { 2, 1 } }
        };
        native.Attributes.Add(Pair("golden.exponential", Text("full")));
        native.Exemplars.Add(new Exemplar { TimeUnixNano = Time, AsDouble = -1.75 });
        var bucketless = new ExponentialHistogramDataPoint { TimeUnixNano = Time };
        bucketless.Attributes.Add(Pair("golden.exponential", Text("empty")));
        var zeroOnly = new ExponentialHistogramDataPoint
        {
            TimeUnixNano = Time, Count = 2, ZeroCount = 2, Scale = -4,
            Positive = new ExponentialHistogramDataPoint.Types.Buckets { Offset = 5 }
        };
        zeroOnly.Attributes.Add(Pair("golden.exponential", Text("zero-only")));
        exponential.ExponentialHistogram.DataPoints.Add([native, bucketless, zeroOnly]);

        var summary = new Metric { Name = "golden.summary", Unit = "ms", Summary = new Summary() };
        var quantiles = new SummaryDataPoint { StartTimeUnixNano = Time - 1_000, TimeUnixNano = Time, Count = 4, Sum = 10.25 };
        quantiles.QuantileValues.Add(
        [
            new SummaryDataPoint.Types.ValueAtQuantile { Quantile = 0, Value = 1 },
            new SummaryDataPoint.Types.ValueAtQuantile { Quantile = 0.5, Value = 2.5 },
            new SummaryDataPoint.Types.ValueAtQuantile { Quantile = 0.99, Value = 3.75 },
            new SummaryDataPoint.Types.ValueAtQuantile { Quantile = 1, Value = 4 }
        ]);
        quantiles.Attributes.Add(Pair("golden.summary", Text("full")));
        var noQuantiles = new SummaryDataPoint { TimeUnixNano = Time };
        noQuantiles.Attributes.Add(Pair("golden.summary", Text("empty")));
        var undefined = new SummaryDataPoint { TimeUnixNano = Time, Count = 1, Sum = double.NaN };
        undefined.QuantileValues.Add(new SummaryDataPoint.Types.ValueAtQuantile { Quantile = 0.5, Value = double.NaN });
        undefined.Attributes.Add(Pair("golden.summary", Text("nan")));
        summary.Summary.DataPoints.Add([quantiles, noQuantiles, undefined]);

        var scope = new ScopeMetrics { Scope = Scope(), SchemaUrl = ScopeSchema };
        scope.Metrics.Add([gauge, sumInt, sumDouble, histogram, exponential, summary]);
        var resource = Resource("golden-metrics", EveryKind("golden.resource"),
            Entity("service", ["service.name"], []),
            Entity("golden.host", ["golden.resource.int.max", "golden.resource.map"], ["golden.resource.text"]));
        var metrics = new ResourceMetrics { Resource = resource, SchemaUrl = "https://opentelemetry.io/schemas/1.39.0" };
        metrics.ScopeMetrics.Add(scope);

        var plain = new Metric { Name = "golden.plain", Gauge = new Gauge() };
        plain.Gauge.DataPoints.Add(new NumberDataPoint { TimeUnixNano = Time, AsInt = 1 });
        var bare = new ScopeMetrics();
        bare.Metrics.Add(plain);
        var bareResource = new ResourceMetrics { Resource = Resource("golden-plain", []) };
        bareResource.ScopeMetrics.Add(bare);

        // One instrument name under each identifying property: kind, temporality, monotonicity, unit
        // and point attributes separate the series of one instrument; the scope name, scope version,
        // resource schema URL and resource attributes each separate the instrument.
        var identityScope = IdentityScope("golden.identity.scope", "1.0.0");
        identityScope.Metrics.Add(
        [
            IdentityGauge("1", "a"), IdentityGauge("1", "b"), IdentityGauge("ms", "a"), IdentityGauge(null, "a"),
            IdentitySum(AggregationTemporality.Cumulative, true), IdentitySum(AggregationTemporality.Cumulative, false),
            IdentitySum(AggregationTemporality.Delta, true), IdentitySum(AggregationTemporality.Delta, false),
            IdentityHistogram()
        ]);
        var otherVersion = IdentityScope("golden.identity.scope", "2.0.0");
        otherVersion.Metrics.Add(IdentityGauge("1", "a"));
        var otherName = IdentityScope("golden.identity.scope.other", "1.0.0");
        otherName.Metrics.Add(IdentityGauge("1", "a"));
        var identity = IdentityResource("one", "https://opentelemetry.io/schemas/1.39.0");
        identity.ScopeMetrics.Add([identityScope, otherVersion, otherName]);
        var otherSchema = IdentityResource("one", "https://opentelemetry.io/schemas/1.38.0");
        var otherHost = IdentityResource("two", "https://opentelemetry.io/schemas/1.39.0");
        foreach (var other in new[] { otherSchema, otherHost })
        {
            var sameScope = IdentityScope("golden.identity.scope", "1.0.0");
            sameScope.Metrics.Add(IdentityGauge("1", "a"));
            other.ScopeMetrics.Add(sameScope);
        }

        var request = new ExportMetricsServiceRequest();
        request.ResourceMetrics.Add([metrics, bareResource, identity, otherSchema, otherHost]);
        return request;

        static ResourceMetrics IdentityResource(string host, string schema) => new()
        {
            Resource = Resource("golden-identity", [Pair("golden.identity.host", Text(host))]), SchemaUrl = schema
        };

        static ScopeMetrics IdentityScope(string name, string version)
        {
            var scope = new ScopeMetrics { Scope = new InstrumentationScope { Name = name, Version = version }, SchemaUrl = ScopeSchema };
            scope.Scope.Attributes.Add(Pair("golden.identity.scope.flag", Flag(true)));
            return scope;
        }

        static Metric IdentityGauge(string? unit, string stream)
        {
            var metric = new Metric { Name = "golden.identity", Gauge = new Gauge() };
            if (unit is not null) metric.Unit = unit;
            metric.Gauge.DataPoints.Add(IdentityPoint(stream));
            return metric;
        }

        static Metric IdentitySum(AggregationTemporality temporality, bool monotonic)
        {
            var metric = new Metric
            {
                Name = "golden.identity", Unit = "1",
                Sum = new Sum { AggregationTemporality = temporality, IsMonotonic = monotonic }
            };
            metric.Sum.DataPoints.Add(IdentityPoint("a"));
            return metric;
        }

        static Metric IdentityHistogram()
        {
            var metric = new Metric
            {
                Name = "golden.identity", Unit = "1",
                Histogram = new Histogram { AggregationTemporality = AggregationTemporality.Delta }
            };
            var point = new HistogramDataPoint { TimeUnixNano = Time + 100, Count = 1, Sum = 1, ExplicitBounds = { 1 }, BucketCounts = { 1, 0 } };
            point.Attributes.Add(Pair("golden.identity.stream", Text("a")));
            metric.Histogram.DataPoints.Add(point);
            return metric;
        }

        static NumberDataPoint IdentityPoint(string stream)
        {
            var point = new NumberDataPoint { TimeUnixNano = Time + 100, AsInt = 1 };
            point.Attributes.Add(Pair("golden.identity.stream", Text(stream)));
            return point;
        }

        static NumberDataPoint NumberPoint(ulong offset, Action<NumberDataPoint> value)
        {
            var point = new NumberDataPoint { TimeUnixNano = Time + offset };
            value(point);
            point.Attributes.Add(EveryKind("golden.point"));
            return point;
        }
    }

    private static ExportLogsServiceRequest Logs()
    {
        var bodies = new (string Name, AnyValue? Body)[]
        {
            ("text", Text("quote\" backslash\\ <tag>&'+` tab\t newline\n control\u0001 é 日本 😀")),
            ("bool", Flag(true)),
            ("int", Integer(long.MinValue)),
            ("double", Number(1e21)),
            ("nan", Number(double.NaN)),
            ("bytes", Bytes(0x00, 0x01, 0xfe, 0xff)),
            ("array", ArrayOf(Text("a"), Integer(1), Number(0.5), None(), MapOf(Pair("z", Flag(false)), Pair("a", Text("first"))))),
            ("map", MapOf(EveryKind("golden.payload"))),
            ("none", null),
            ("long-text", Text(string.Concat(Enumerable.Repeat("é日😀x", 20_000)))),
            ("long-map", MapOf(Pair("then.filler", Text(new string('x', 70_000))), Pair("first", MapOf(EveryKind("golden.payload")))))
        };
        var records = new ScopeLogs { Scope = Scope() };
        for (var index = 0; index < bodies.Length; index++)
        {
            var record = new LogRecord
            {
                TimeUnixNano = Time + (ulong)index, ObservedTimeUnixNano = Time + (ulong)index + 1,
                SeverityNumber = SeverityNumber.Warn, SeverityText = "Warning", EventName = "golden." + bodies[index].Name,
                TraceId = Id(16, 0x10), SpanId = Id(8, 0x80)
            };
            if (bodies[index].Body is { } body) record.Body = body;
            record.Attributes.Add(index == 0 ? EveryKind("golden.log") : [Pair("golden.log.case", Text(bodies[index].Name))]);
            records.LogRecords.Add(record);
        }

        var main = new ResourceLogs
        {
            Resource = Resource("golden-logs", EveryKind("golden.resource"),
                Entity("service", ["service.name"], []),
                Entity("golden.text", ["golden.resource.text"], []),
                Entity("golden.bool", ["golden.resource.bool.true"], []),
                Entity("golden.int", ["golden.resource.int.min"], []),
                Entity("golden.double", ["golden.resource.double.small", "golden.resource.double.large"], []),
                Entity("golden.bytes", ["golden.resource.bytes"], []),
                Entity("golden.array", ["golden.resource.array"], []),
                Entity("golden.map", ["golden.resource.map"], ["golden.resource.text"]),
                Entity("golden.none", ["golden.resource.none"], []))
        };
        main.ScopeLogs.Add(records);
        var request = new ExportLogsServiceRequest();
        request.ResourceLogs.Add(main);

        // One entity per value kind, so each canonical identity text reaches its own log identifier.
        foreach (var (name, value) in EveryKind("golden.entity").Select(static pair => (pair.Key, pair.Value)))
        {
            var entity = new ResourceLogs
            {
                Resource = Resource("golden-entity", [Pair("golden.entity.value", value)],
                    Entity("golden.entity", ["golden.entity.value"], []))
            };
            var scope = new ScopeLogs();
            scope.LogRecords.Add(new LogRecord
            {
                TimeUnixNano = Time, ObservedTimeUnixNano = Time, Body = Text(name), SeverityNumber = SeverityNumber.Info
            });
            entity.ScopeLogs.Add(scope);
            request.ResourceLogs.Add(entity);
        }

        // Without either timestamp the request ordinal joins the identity, so identical untimed
        // records stay distinct; a stamped receive time never joins it.
        var untimed = new ScopeLogs();
        untimed.LogRecords.Add(
        [
            Untimed("golden.untimed.repeated", 0, 0), Untimed("golden.untimed.repeated", 0, 0),
            Untimed("golden.untimed.event-time-only", Time + 50, 0), Untimed("golden.untimed.observed-only", 0, Time + 60)
        ]);
        untimed.LogRecords[0].Attributes.Add(Pair("golden.untimed.case", Text("first")));
        untimed.LogRecords[1].Attributes.Add(Pair("golden.untimed.case", Text("first")));
        var untimedResource = new ResourceLogs { Resource = Resource("golden-untimed", []) };
        untimedResource.ScopeLogs.Add(untimed);
        request.ResourceLogs.Add(untimedResource);
        return request;

        static LogRecord Untimed(string eventName, ulong time, ulong observed) => new()
        {
            TimeUnixNano = time, ObservedTimeUnixNano = observed, EventName = eventName,
            Body = Text("untimed"), SeverityNumber = SeverityNumber.Info
        };
    }

    private static ExportTraceServiceRequest Traces()
    {
        var root = new OtlpSpan
        {
            TraceId = ByteString.CopyFrom(Convert.FromHexString(TraceHex)), SpanId = Id(8, 0x90), Name = "golden.root",
            Kind = OtlpSpan.Types.SpanKind.Server, StartTimeUnixNano = Time, EndTimeUnixNano = Time + 1_000,
            TraceState = "golden=1", Flags = 1
        };
        root.Attributes.Add(EveryKind("golden.span"));
        root.Attributes.Add(
        [
            Pair("gen_ai.operation.name", Text(" Chat ")), Pair("gen_ai.output_type", Text("JSON")),
            Pair("gen_ai.request.model", Text("golden-model"))
        ]);
        var annotated = new OtlpSpan.Types.Event { Name = "golden.event", TimeUnixNano = Time + 10, DroppedAttributesCount = 2 };
        annotated.Attributes.Add(EveryKind("golden.event"));
        root.Events.Add([annotated, new OtlpSpan.Types.Event { Name = "golden.event.bare", TimeUnixNano = Time + 20 }]);
        var link = new OtlpSpan.Types.Link
        {
            TraceId = Id(16, 0xa0), SpanId = Id(8, 0xb0), TraceState = "linked=1", Flags = 1, DroppedAttributesCount = 3
        };
        link.Attributes.Add(EveryKind("golden.link"));
        root.Links.Add([link, new OtlpSpan.Types.Link { TraceId = Id(16, 0xc0), SpanId = Id(8, 0xd0) }]);
        var child = new OtlpSpan
        {
            TraceId = root.TraceId, SpanId = Id(8, 0xe0), ParentSpanId = root.SpanId, Name = "golden.child",
            StartTimeUnixNano = Time + 100, EndTimeUnixNano = Time + 200
        };
        child.Attributes.Add(Pair("golden.child", ArrayOf(Integer(1), Integer(2))));

        var scope = new ScopeSpans { Scope = Scope() };
        scope.Spans.Add([root, child]);
        var resource = new ResourceSpans
        {
            Resource = Resource("golden-traces", EveryKind("golden.resource"), Entity("service", ["service.name"], []))
        };
        resource.ScopeSpans.Add(scope);
        var request = new ExportTraceServiceRequest();
        request.ResourceSpans.Add(resource);
        return request;
    }

    private static KeyValue[] EveryKind(string prefix) =>
    [
        Pair(prefix + ".text", Text("quote\" backslash\\ <tag>&'+` tab\t newline\n control\u0001 é 日本 😀")),
        Pair(prefix + ".bool.true", Flag(true)),
        Pair(prefix + ".bool.false", Flag(false)),
        Pair(prefix + ".int.max", Integer(long.MaxValue)),
        Pair(prefix + ".int.min", Integer(long.MinValue)),
        Pair(prefix + ".int.zero", Integer(0)),
        Pair(prefix + ".double.fraction", Number(0.1)),
        Pair(prefix + ".double.integral", Number(42)),
        Pair(prefix + ".double.large", Number(1e21)),
        Pair(prefix + ".double.small", Number(1.5e-7)),
        Pair(prefix + ".double.negative_zero", Number(-0.0)),
        Pair(prefix + ".double.max", Number(double.MaxValue)),
        Pair(prefix + ".double.epsilon", Number(double.Epsilon)),
        Pair(prefix + ".double.nan", Number(double.NaN)),
        Pair(prefix + ".double.infinity", Number(double.PositiveInfinity)),
        Pair(prefix + ".double.negative_infinity", Number(double.NegativeInfinity)),
        Pair(prefix + ".bytes", Bytes(0x00, 0xff, 0x10, 0x7f)),
        Pair(prefix + ".bytes.empty", Bytes()),
        Pair(prefix + ".array", ArrayOf(Text("a"), Integer(-1), Number(2.5), Flag(false), None(), ArrayOf(),
            ArrayOf(Text("nested")), MapOf(Pair("inside", Integer(1))), Bytes(1))),
        Pair(prefix + ".array.empty", ArrayOf()),
        Pair(prefix + ".map", MapOf(Pair("zeta", Integer(1)), Pair("Alpha", Text("upper")), Pair("alpha", Number(0.5)),
            Pair("void", None()), Pair("inner", MapOf(Pair("b", Flag(true)), Pair("a", ArrayOf(Integer(2))))),
            Pair("é", Text("escaped key")))),
        Pair(prefix + ".map.empty", MapOf()),
        Pair(prefix + ".none", None()),
        Pair(prefix + ".ключ.😀", Text("non-ASCII key"))
    ];

    private static OtlpResource Resource(string service, KeyValue[] attributes, params EntityRef[] entities)
    {
        var resource = new OtlpResource();
        resource.Attributes.Add(Pair("service.name", Text(service)));
        resource.Attributes.Add(attributes);
        resource.EntityRefs.Add(entities);
        return resource;
    }

    private static EntityRef Entity(string type, string[] ids, string[] descriptions)
    {
        var entity = new EntityRef { Type = type, SchemaUrl = "https://opentelemetry.io/schemas/1.40.0" };
        entity.IdKeys.Add(ids);
        entity.DescriptionKeys.Add(descriptions);
        return entity;
    }

    private static InstrumentationScope Scope()
    {
        var scope = new InstrumentationScope { Name = "golden.scope", Version = "1.2.3", DroppedAttributesCount = 1 };
        scope.Attributes.Add(EveryKind("golden.scope"));
        return scope;
    }

    private static ByteString Id(int length, byte first) =>
        ByteString.CopyFrom(Enumerable.Range(0, length).Select(index => (byte)(first + index)).ToArray());

    private static KeyValue Pair(string key, AnyValue value) => new() { Key = key, Value = value };

    private static AnyValue Text(string value) => new() { StringValue = value };

    private static AnyValue Flag(bool value) => new() { BoolValue = value };

    private static AnyValue Integer(long value) => new() { IntValue = value };

    private static AnyValue Number(double value) => new() { DoubleValue = value };

    private static AnyValue Bytes(params byte[] value) => new() { BytesValue = ByteString.CopyFrom(value) };

    private static AnyValue None() => new();

    private static AnyValue ArrayOf(params AnyValue[] items)
    {
        var array = new ArrayValue();
        array.Values.Add(items);
        return new AnyValue { ArrayValue = array };
    }

    private static AnyValue MapOf(params KeyValue[] entries)
    {
        var map = new KeyValueList();
        map.Values.Add(entries);
        return new AnyValue { KvlistValue = map };
    }
}
