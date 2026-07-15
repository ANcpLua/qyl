using System.Text.Json;
using DuckDB.NET.Data;
using Google.Protobuf;
using OpenTelemetry.Proto.Collector.Metrics.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Metrics.V1;
using OpenTelemetry.Proto.Resource.V1;
using Qyl.Api.Contracts.OTel.Enums;
using Qyl.Api.Contracts.OTel.Metrics;
using Qyl.Collector.Ingestion;
using Qyl.Collector.Mapping;
using Qyl.Collector.Storage;
using ContractAggregationTemporality = Qyl.Api.Contracts.OTel.Enums.AggregationTemporality;
using ProtoAggregationTemporality = OpenTelemetry.Proto.Metrics.V1.AggregationTemporality;
using ProtoInstrumentationScope = OpenTelemetry.Proto.Common.V1.InstrumentationScope;

namespace Qyl.Collector.Tests;

public sealed class MetricContractProjectionTests
{
    [Fact]
    public async Task Every_metric_variant_preserves_the_generated_contract_projection()
    {
        var request = BuildCompleteRequest();
        request.ResourceMetrics[0].Resource.EntityRefs.Add(new EntityRef
        {
            SchemaUrl = "https://opentelemetry.io/schemas/1.38.0",
            Type = "service",
            IdKeys = { "service.name" },
            DescriptionKeys = { "service.namespace" }
        });
        var rows = IngestionStorageMapper.ToMetricStorageRows(OtlpConverter.ConvertMetrics(request));
        Assert.Equal(5, rows.Count);
        Assert.All(rows, static row =>
        {
            Assert.Equal(MetricStorageRow.CurrentContractProjectionVersion, row.ContractProjectionVersion);
            Assert.NotNull(row.StartTimeUnixNano);
            Assert.NotNull(row.Flags);
            Assert.NotNull(row.ResourceDroppedAttributesCount);
            Assert.NotNull(row.HasInstrumentationScope);
            Assert.NotNull(row.ScopeDroppedAttributesCount);
        });

        await using var store = new DuckDbStore(":memory:");
        await store.InsertMetricsAsync(rows, TestContext.Current.CancellationToken);
        var stored = await store.GetMetricsAsync("default", ct: TestContext.Current.CancellationToken);
        var contracts = stored.ToDictionary(
            static row => row.MetricName,
            static row => MetricMapper.ToContract(row),
            StringComparer.Ordinal);

        var gauge = Assert.IsType<GaugeMetricPoint>(contracts["test.gauge"]);
        Assert.Equal(long.MaxValue, Assert.IsType<MetricIntegerValue>(gauge.Value).AsInt);
        Assert.Equal<ulong>(0, gauge.StartTimeUnixNano);
        Assert.Equal<ulong>(100, gauge.TimeUnixNano);
        Assert.Equal<uint>(2, gauge.Flags);
        Assert.Equal("https://opentelemetry.io/schemas/1.38.0", gauge.ResourceSchemaUrl);
        Assert.Equal(2, gauge.Resource.DroppedAttributesCount);
        var entityRef = Assert.Single(gauge.Resource.EntityRefs!);
        Assert.Equal("https://opentelemetry.io/schemas/1.38.0", entityRef.SchemaUrl);
        Assert.Equal("service", entityRef.Type);
        Assert.Equal(["service.name"], entityRef.IdKeys);
        Assert.Equal(["service.namespace"], entityRef.DescriptionKeys);
        Assert.Contains(gauge.Metadata!, static attribute =>
            attribute.Key == "translation.schema" && Equals(attribute.Value, "v1"));
        var scope = Assert.IsType<Qyl.Api.Contracts.Common.InstrumentationScope>(gauge.InstrumentationScope);
        Assert.Equal("test.scope", scope.ScopeName);
        Assert.Equal("1.2.3", scope.ScopeVersion);
        Assert.Equal(3, scope.DroppedAttributesCount);
        Assert.Contains(scope.ScopeAttributes!, static attribute =>
            attribute.Key == "scope.custom" && Equals(attribute.Value, "scope-value"));
        Assert.Equal("https://opentelemetry.io/schemas/1.39.0", gauge.ScopeSchemaUrl);
        var exemplar = Assert.Single(gauge.Exemplars!);
        Assert.Equal(-7, Assert.IsType<MetricIntegerValue>(exemplar.Value).AsInt);
        Assert.Equal("0101010101010101", exemplar.SpanId);
        Assert.Equal("02020202020202020202020202020202", exemplar.TraceId);
        Assert.Contains(exemplar.FilteredAttributes!, static attribute =>
            attribute.Key == "exemplar.custom" && Equals(attribute.Value, "filtered"));

        var sum = Assert.IsType<SumMetricPoint>(contracts["test.sum"]);
        Assert.Equal(1.5, Assert.IsType<MetricDoubleValue>(sum.Value).AsDouble);
        Assert.Equal(ContractAggregationTemporality.Delta, sum.AggregationTemporality);
        Assert.True(sum.IsMonotonic);

        var histogram = Assert.IsType<HistogramMetricPoint>(contracts["test.histogram"]);
        Assert.Equal<ulong>(4, histogram.Count);
        Assert.Equal([1UL, 2UL, 1UL], histogram.BucketCounts);
        Assert.Equal([0.1, 1.0], histogram.ExplicitBounds);
        Assert.Equal(0.05, histogram.Min);
        Assert.Equal(2.0, histogram.Max);
        Assert.Equal(ContractAggregationTemporality.Cumulative, histogram.AggregationTemporality);

        var exponential = Assert.IsType<ExponentialHistogramMetricPoint>(contracts["test.exponential"]);
        Assert.Equal(-2, exponential.Scale);
        Assert.Equal<ulong>(1, exponential.ZeroCount);
        Assert.Equal(0.001, exponential.ZeroThreshold);
        Assert.Equal(4, exponential.Positive.Offset);
        Assert.Equal([2UL, 3UL], exponential.Positive.BucketCounts);
        Assert.Equal(-6, exponential.Negative.Offset);
        Assert.Equal([1UL], exponential.Negative.BucketCounts);

        var summary = Assert.IsType<SummaryMetricPoint>(contracts["test.summary"]);
        Assert.Equal<ulong>(10, summary.Count);
        Assert.Equal(25.0, summary.Sum);
        Assert.Collection(
            summary.QuantileValues,
            value =>
            {
                Assert.Equal(0.5, value.Quantile);
                Assert.Equal(2.0, value.Value);
            },
            value =>
            {
                Assert.Equal(0.9, value.Quantile);
                Assert.Equal(4.0, value.Value);
            });
        Assert.Equal<uint>(2, summary.Flags);
    }

    [Fact]
    public void Metric_identity_is_stable_but_keeps_distinct_stream_semantics_and_attribute_types()
    {
        var baseline = BuildCompleteRequest();
        var baselineRows = RowsByName(baseline);

        var reordered = baseline.Clone();
        var resourceAttributes = reordered.ResourceMetrics[0].Resource.Attributes;
        (resourceAttributes[0], resourceAttributes[1]) = (resourceAttributes[1], resourceAttributes[0]);
        Assert.Equal(baselineRows["test.gauge"].MetricId, RowsByName(reordered)["test.gauge"].MetricId);

        var differentMonotonicity = baseline.Clone();
        differentMonotonicity.ResourceMetrics[0].ScopeMetrics[0].Metrics
            .Single(static metric => metric.Name == "test.sum").Sum.IsMonotonic = false;
        Assert.NotEqual(
            baselineRows["test.sum"].MetricId,
            RowsByName(differentMonotonicity)["test.sum"].MetricId);

        var differentTemporality = baseline.Clone();
        differentTemporality.ResourceMetrics[0].ScopeMetrics[0].Metrics
            .Single(static metric => metric.Name == "test.sum").Sum.AggregationTemporality =
            ProtoAggregationTemporality.Cumulative;
        Assert.NotEqual(
            baselineRows["test.sum"].MetricId,
            RowsByName(differentTemporality)["test.sum"].MetricId);

        var differentGaugeStart = baseline.Clone();
        differentGaugeStart.ResourceMetrics[0].ScopeMetrics[0].Metrics
            .Single(static metric => metric.Name == "test.gauge").Gauge.DataPoints[0].StartTimeUnixNano = 99;
        Assert.Equal(
            baselineRows["test.gauge"].MetricId,
            RowsByName(differentGaugeStart)["test.gauge"].MetricId);

        var integerAttribute = baseline.Clone();
        integerAttribute.ResourceMetrics[0].ScopeMetrics[0].Metrics
            .Single(static metric => metric.Name == "test.gauge").Gauge.DataPoints[0].Attributes.Add(
                new KeyValue { Key = "custom.numeric", Value = new AnyValue { IntValue = 1 } });
        var doubleAttribute = integerAttribute.Clone();
        doubleAttribute.ResourceMetrics[0].ScopeMetrics[0].Metrics
            .Single(static metric => metric.Name == "test.gauge").Gauge.DataPoints[0].Attributes[0].Value =
            new AnyValue { DoubleValue = 1 };
        Assert.NotEqual(
            RowsByName(integerAttribute)["test.gauge"].MetricId,
            RowsByName(doubleAttribute)["test.gauge"].MetricId);

        var entityReferences = baseline.Clone();
        entityReferences.ResourceMetrics[0].Resource.EntityRefs.Add(
            new EntityRef { Type = "service", IdKeys = { "service.name" } });
        entityReferences.ResourceMetrics[0].Resource.EntityRefs.Add(
            new EntityRef { Type = "namespace", IdKeys = { "service.namespace" } });
        var reorderedEntityReferences = entityReferences.Clone();
        (reorderedEntityReferences.ResourceMetrics[0].Resource.EntityRefs[0],
            reorderedEntityReferences.ResourceMetrics[0].Resource.EntityRefs[1]) =
            (reorderedEntityReferences.ResourceMetrics[0].Resource.EntityRefs[1],
                reorderedEntityReferences.ResourceMetrics[0].Resource.EntityRefs[0]);
        Assert.NotEqual(
            baselineRows["test.gauge"].MetricId,
            RowsByName(entityReferences)["test.gauge"].MetricId);
        Assert.Equal(
            RowsByName(entityReferences)["test.gauge"].MetricId,
            RowsByName(reorderedEntityReferences)["test.gauge"].MetricId);

        var descriptionChanged = entityReferences.Clone();
        descriptionChanged.ResourceMetrics[0].Resource.EntityRefs[0].DescriptionKeys.Add("service.namespace");
        Assert.Equal(
            RowsByName(entityReferences)["test.gauge"].MetricId,
            RowsByName(descriptionChanged)["test.gauge"].MetricId);

        var schemaChanged = entityReferences.Clone();
        schemaChanged.ResourceMetrics[0].Resource.EntityRefs[0].SchemaUrl =
            "https://opentelemetry.io/schemas/1.38.0";
        Assert.Equal(
            RowsByName(entityReferences)["test.gauge"].MetricId,
            RowsByName(schemaChanged)["test.gauge"].MetricId);
    }

    [Fact]
    public void Named_floating_point_values_survive_storage_and_contract_json()
    {
        var request = BuildCompleteRequest();
        var gaugePoint = request.ResourceMetrics[0].ScopeMetrics[0].Metrics
            .Single(static metric => metric.Name == "test.gauge").Gauge.DataPoints[0];
        gaugePoint.AsDouble = double.NaN;
        gaugePoint.Exemplars[0].AsDouble = double.PositiveInfinity;
        gaugePoint.Attributes.Add(new KeyValue { Key = "http.request.method", Value = new AnyValue() });
        request.ResourceMetrics[0].ScopeMetrics[0].Metrics
            .Single(static metric => metric.Name == "test.summary").Summary.DataPoints[0]
            .QuantileValues[0].Value = double.PositiveInfinity;

        var contract = MetricMapper.ToContract(RowsByName(request)["test.gauge"]);
        var json = JsonSerializer.Serialize(contract, QylSerializerContext.Default.MetricPoint);

        Assert.Contains("\"as_double\":\"NaN\"", json, StringComparison.Ordinal);
        Assert.Contains("\"as_double\":\"Infinity\"", json, StringComparison.Ordinal);
        Assert.Contains("{\"key\":\"http.request.method\",\"value\":null}", json, StringComparison.Ordinal);
        var summary = MetricMapper.ToContract(RowsByName(request)["test.summary"]);
        var summaryJson = JsonSerializer.Serialize(summary, QylSerializerContext.Default.MetricPoint);
        Assert.Contains("\"value\":\"Infinity\"", summaryJson, StringComparison.Ordinal);
    }

    [Fact]
    public void Tagged_attribute_values_are_reconstructed_without_type_loss_and_malformed_rows_fail_closed()
    {
        var request = BuildCompleteRequest();
        var point = request.ResourceMetrics[0].ScopeMetrics[0].Metrics
            .Single(static metric => metric.Name == "test.gauge").Gauge.DataPoints[0];
        point.Attributes.Add(new KeyValue
        {
            Key = "db.cassandra.page_size",
            Value = new AnyValue { IntValue = long.MaxValue }
        });
        point.Attributes.Add(new KeyValue
        {
            Key = "db.client.connections.state",
            Value = new AnyValue { DoubleValue = double.PositiveInfinity }
        });
        point.Attributes.Add(new KeyValue
        {
            Key = "db.collection.name",
            Value = new AnyValue { BytesValue = ByteString.CopyFrom([0, 127, 255]) }
        });
        point.Attributes.Add(new KeyValue
        {
            Key = "db.namespace",
            Value = new AnyValue
            {
                ArrayValue = new ArrayValue
                {
                    Values =
                    {
                        new AnyValue { IntValue = 7 },
                        new AnyValue { DoubleValue = double.NaN }
                    }
                }
            }
        });
        point.Attributes.Add(new KeyValue
        {
            Key = "dotnet.gc.heap.generation",
            Value = new AnyValue
            {
                KvlistValue = new KeyValueList
                {
                    Values =
                    {
                        new KeyValue { Key = "nested", Value = new AnyValue { IntValue = -9 } }
                    }
                }
            }
        });

        var row = RowsByName(request)["test.gauge"];
        var gauge = Assert.IsType<GaugeMetricPoint>(MetricMapper.ToContract(row));
        var attributes = gauge.Attributes!.ToDictionary(static item => item.Key, static item => item.Value);
        Assert.Equal(long.MaxValue, Assert.IsType<Qyl.Api.Contracts.Common.AttributeIntValue>(
            attributes["db.cassandra.page_size"]).Value);
        Assert.Equal(double.PositiveInfinity, Assert.IsType<Qyl.Api.Contracts.Common.AttributeDoubleValue>(
            attributes["db.client.connections.state"]).Value);
        Assert.Equal(
            new byte[] { 0, 127, 255 },
            Assert.IsType<Qyl.Api.Contracts.Common.AttributeBytesValue>(attributes["db.collection.name"]).Base64.ToArray());
        var array = Assert.IsType<object?[]>(attributes["db.namespace"]);
        Assert.Equal(7, Assert.IsType<Qyl.Api.Contracts.Common.AttributeIntValue>(array[0]).Value);
        Assert.True(double.IsNaN(Assert.IsType<Qyl.Api.Contracts.Common.AttributeDoubleValue>(array[1]).Value));
        var kvlist = Assert.IsType<Qyl.Api.Contracts.Common.AttributeKeyValueListValue>(
            attributes["dotnet.gc.heap.generation"]);
        Assert.Equal(-9, Assert.IsType<Qyl.Api.Contracts.Common.AttributeIntValue>(kvlist.Values["nested"]).Value);

        Assert.False(MetricMapper.TryToContract(row with { AttributesJson = """{"bad":42}""" }, out _));
        Assert.False(MetricMapper.TryToContract(
            row with { AttributesJson = """{"bad":{"type":"int","value":1}}""" },
            out _));
        Assert.False(MetricMapper.TryToContract(
            row with { AttributesJson = """{"bad":{"type":"bytes","base64":"***"}}""" },
            out _));
    }

    [Fact]
    public void No_recorded_value_points_discard_measurements_and_exemplars()
    {
        var request = BuildCompleteRequest();
        var metrics = request.ResourceMetrics[0].ScopeMetrics[0].Metrics;
        metrics.Single(static metric => metric.Name == "test.gauge").Gauge.DataPoints[0].Flags = 1;
        metrics.Single(static metric => metric.Name == "test.sum").Sum.DataPoints[0].Flags = 1;
        metrics.Single(static metric => metric.Name == "test.histogram").Histogram.DataPoints[0].Flags = 1;
        metrics.Single(static metric => metric.Name == "test.exponential").ExponentialHistogram.DataPoints[0].Flags = 1;
        metrics.Single(static metric => metric.Name == "test.summary").Summary.DataPoints[0].Flags = 1;

        var rows = RowsByName(request);
        Assert.Null(rows["test.gauge"].IntValue);
        Assert.Null(rows["test.gauge"].DoubleValue);
        Assert.Null(rows["test.gauge"].ExemplarsJson);
        Assert.Equal<ulong?>(0, rows["test.histogram"].Count);
        Assert.Equal<ulong?>(0, rows["test.exponential"].Count);
        Assert.Equal<ulong?>(0, rows["test.summary"].Count);
        Assert.Equal<double?>(0, rows["test.summary"].Sum);

        Assert.Null(Assert.IsType<GaugeMetricPoint>(MetricMapper.ToContract(rows["test.gauge"])).Value);
        Assert.Null(Assert.IsType<SumMetricPoint>(MetricMapper.ToContract(rows["test.sum"])).Value);
        Assert.Empty(Assert.IsType<SummaryMetricPoint>(MetricMapper.ToContract(rows["test.summary"])).QuantileValues);

        Assert.False(MetricMapper.TryToContract(
            rows["test.histogram"] with { Sum = 0 },
            out _));
        Assert.False(MetricMapper.TryToContract(
            rows["test.exponential"] with { ExponentialHistogramZeroThreshold = 0.001 },
            out _));
        Assert.False(MetricMapper.TryToContract(
            rows["test.summary"] with
            {
                SummaryQuantilesJson = """[{"quantile":0.5,"value":0}]"""
            },
            out _));
    }

    [Fact]
    public void Legacy_lossy_metric_rows_are_not_fabricated_as_public_contracts()
    {
        var legacy = new MetricStorageRow
        {
            ProjectId = "default",
            MetricId = "metric_00000000000000000000000000000000",
            MetricName = "legacy.gauge",
            MetricType = MetricStorageTypes.Gauge,
            TimeUnixNano = 1,
            Value = 42,
            ServiceName = "legacy-service"
        };

        Assert.False(MetricMapper.TryToContract(legacy, out _));
    }

    [Fact]
    public async Task Existing_metric_tables_gain_entity_references_and_round_trip_projection_v2()
    {
        var databasePath = Path.Combine(
            Path.GetTempPath(),
            $"qyl-metric-entity-ref-migration-{Guid.NewGuid():N}.duckdb");
        try
        {
            await using (var initialized = new DuckDbStore(databasePath, maxConcurrentReads: 1))
            {
            }

            await using (var connection = new DuckDBConnection($"DataSource={databasePath}"))
            {
                await connection.OpenAsync(TestContext.Current.CancellationToken);
                var indexNames = new List<string>();
                await using (var listIndexes = connection.CreateCommand())
                {
                    listIndexes.CommandText =
                        "SELECT index_name FROM duckdb_indexes() WHERE table_name = 'metrics'";
                    await using var reader = await listIndexes.ExecuteReaderAsync(
                        TestContext.Current.CancellationToken);
                    while (await reader.ReadAsync(TestContext.Current.CancellationToken))
                        indexNames.Add(reader.GetString(0));
                }

                foreach (var indexName in indexNames)
                {
                    await using var dropIndex = connection.CreateCommand();
                    dropIndex.CommandText = $"DROP INDEX \"{indexName.Replace("\"", "\"\"")}\"";
                    await dropIndex.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
                }

                await using var dropColumn = connection.CreateCommand();
                dropColumn.CommandText = "ALTER TABLE metrics DROP COLUMN resource_entity_refs_json";
                await dropColumn.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            }

            var request = BuildCompleteRequest();
            request.ResourceMetrics[0].Resource.EntityRefs.Add(new EntityRef
            {
                Type = "service",
                IdKeys = { "service.name" },
                DescriptionKeys = { "service.namespace" }
            });
            var row = RowsByName(request)["test.gauge"];
            Assert.Equal<byte?>((byte)2, row.ContractProjectionVersion);

            await using var migrated = new DuckDbStore(databasePath, maxConcurrentReads: 1);
            await migrated.InsertMetricsAsync([row], TestContext.Current.CancellationToken);
            var stored = Assert.Single(await migrated.GetMetricsAsync(
                "default",
                ct: TestContext.Current.CancellationToken));
            var contract = Assert.IsType<GaugeMetricPoint>(MetricMapper.ToContract(stored));
            Assert.Equal("service", Assert.Single(contract.Resource.EntityRefs!).Type);
        }
        finally
        {
            File.Delete(databasePath);
            File.Delete($"{databasePath}.wal");
        }
    }

    [Fact]
    public void Invalid_points_are_rejected_before_the_lossless_projection_marker_is_written()
    {
        var request = new ExportMetricsServiceRequest
        {
            ResourceMetrics =
            {
                new ResourceMetrics
                {
                    ScopeMetrics =
                    {
                        new ScopeMetrics
                        {
                            Metrics =
                            {
                                new Metric
                                {
                                    Name = "invalid.sum",
                                    Sum = new Sum
                                    {
                                        AggregationTemporality = ProtoAggregationTemporality.Unspecified,
                                        DataPoints = { new NumberDataPoint { TimeUnixNano = 1, AsInt = 1 } }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };

        Assert.Throws<InvalidDataException>(() => OtlpConverter.ConvertMetrics(request));

        var missingNumber = BuildCompleteRequest();
        missingNumber.ResourceMetrics[0].ScopeMetrics[0].Metrics
            .Single(static metric => metric.Name == "test.gauge").Gauge.DataPoints[0].ClearAsInt();
        Assert.Throws<InvalidDataException>(() => OtlpConverter.ConvertMetrics(missingNumber));
    }

    [Fact]
    public void Invalid_metric_distributions_attributes_and_entity_references_are_rejected()
    {
        AssertInvalid(request =>
            request.ResourceMetrics[0].ScopeMetrics[0].Metrics
                .Single(static metric => metric.Name == "test.histogram")
                .Histogram.DataPoints[0].BucketCounts[0]++);
        AssertInvalid(request =>
            request.ResourceMetrics[0].ScopeMetrics[0].Metrics
                .Single(static metric => metric.Name == "test.histogram")
                .Histogram.DataPoints[0].ExplicitBounds[1] = 0.1);
        AssertInvalid(request =>
            request.ResourceMetrics[0].ScopeMetrics[0].Metrics
                .Single(static metric => metric.Name == "test.exponential")
                .ExponentialHistogram.DataPoints[0].ZeroCount++);
        AssertInvalid(request =>
            request.ResourceMetrics[0].ScopeMetrics[0].Metrics
                .Single(static metric => metric.Name == "test.summary")
                .Summary.DataPoints[0].QuantileValues[0].Value = -1);
        AssertInvalid(request =>
        {
            var point = request.ResourceMetrics[0].ScopeMetrics[0].Metrics
                .Single(static metric => metric.Name == "test.gauge").Gauge.DataPoints[0];
            point.Attributes.Add(StringAttribute("duplicate", "one"));
            point.Attributes.Add(StringAttribute("duplicate", "two"));
        });
        AssertInvalid(request => request.ResourceMetrics[0].Resource.EntityRefs.Add(
            new EntityRef { Type = "service", IdKeys = { "missing.attribute" } }));
        AssertInvalid(request =>
        {
            request.ResourceMetrics[0].Resource.EntityRefs.Add(new EntityRef
            {
                Type = "service",
                IdKeys = { "service.name" }
            });
            request.ResourceMetrics[0].Resource.EntityRefs.Add(new EntityRef
            {
                SchemaUrl = "https://opentelemetry.io/schemas/1.38.0",
                Type = "service",
                IdKeys = { "service.name" },
                DescriptionKeys = { "service.namespace" }
            });
        });
    }

    private static Dictionary<string, MetricStorageRow> RowsByName(ExportMetricsServiceRequest request) =>
        IngestionStorageMapper.ToMetricStorageRows(OtlpConverter.ConvertMetrics(request))
            .ToDictionary(static row => row.MetricName, StringComparer.Ordinal);

    private static void AssertInvalid(Action<ExportMetricsServiceRequest> mutate)
    {
        var request = BuildCompleteRequest();
        mutate(request);
        Assert.Throws<InvalidDataException>(() => OtlpConverter.ConvertMetrics(request));
    }

    private static ExportMetricsServiceRequest BuildCompleteRequest()
    {
        var scopeMetrics = new ScopeMetrics
        {
            SchemaUrl = "https://opentelemetry.io/schemas/1.39.0",
            Scope = new ProtoInstrumentationScope
            {
                Name = "test.scope",
                Version = "1.2.3",
                DroppedAttributesCount = 3,
                Attributes =
                {
                    StringAttribute("scope.custom", "scope-value")
                }
            }
        };

        var gaugePoint = new NumberDataPoint
        {
            StartTimeUnixNano = 0,
            TimeUnixNano = 100,
            Flags = 2,
            AsInt = long.MaxValue,
            Exemplars =
            {
                new Exemplar
                {
                    TimeUnixNano = 99,
                    AsInt = -7,
                    SpanId = ByteString.CopyFrom(Enumerable.Repeat((byte)1, 8).ToArray()),
                    TraceId = ByteString.CopyFrom(Enumerable.Repeat((byte)2, 16).ToArray()),
                    FilteredAttributes = { StringAttribute("exemplar.custom", "filtered") }
                }
            }
        };
        scopeMetrics.Metrics.Add(new Metric
        {
            Name = "test.gauge",
            Description = "exact integer gauge",
            Unit = "1",
            Metadata = { StringAttribute("translation.schema", "v1") },
            Gauge = new Gauge { DataPoints = { gaugePoint } }
        });

        scopeMetrics.Metrics.Add(new Metric
        {
            Name = "test.sum",
            Sum = new Sum
            {
                AggregationTemporality = ProtoAggregationTemporality.Delta,
                IsMonotonic = true,
                DataPoints =
                {
                    new NumberDataPoint
                    {
                        StartTimeUnixNano = 10,
                        TimeUnixNano = 110,
                        AsDouble = 1.5
                    }
                }
            }
        });

        scopeMetrics.Metrics.Add(new Metric
        {
            Name = "test.histogram",
            Histogram = new Histogram
            {
                AggregationTemporality = ProtoAggregationTemporality.Cumulative,
                DataPoints =
                {
                    new HistogramDataPoint
                    {
                        StartTimeUnixNano = 10,
                        TimeUnixNano = 120,
                        Count = 4,
                        Sum = 3.25,
                        Min = 0.05,
                        Max = 2.0,
                        ExplicitBounds = { 0.1, 1.0 },
                        BucketCounts = { 1, 2, 1 }
                    }
                }
            }
        });

        scopeMetrics.Metrics.Add(new Metric
        {
            Name = "test.exponential",
            ExponentialHistogram = new ExponentialHistogram
            {
                AggregationTemporality = ProtoAggregationTemporality.Delta,
                DataPoints =
                {
                    new ExponentialHistogramDataPoint
                    {
                        StartTimeUnixNano = 10,
                        TimeUnixNano = 130,
                        Count = 7,
                        Sum = 12.5,
                        Scale = -2,
                        ZeroCount = 1,
                        ZeroThreshold = 0.001,
                        Positive = new ExponentialHistogramDataPoint.Types.Buckets
                        {
                            Offset = 4,
                            BucketCounts = { 2, 3 }
                        },
                        Negative = new ExponentialHistogramDataPoint.Types.Buckets
                        {
                            Offset = -6,
                            BucketCounts = { 1 }
                        }
                    }
                }
            }
        });

        scopeMetrics.Metrics.Add(new Metric
        {
            Name = "test.summary",
            Summary = new Summary
            {
                DataPoints =
                {
                    new SummaryDataPoint
                    {
                        StartTimeUnixNano = 0,
                        TimeUnixNano = 140,
                        Flags = 2,
                        Count = 10,
                        Sum = 25,
                        QuantileValues =
                        {
                            new SummaryDataPoint.Types.ValueAtQuantile { Quantile = 0.5, Value = 2 },
                            new SummaryDataPoint.Types.ValueAtQuantile { Quantile = 0.9, Value = 4 }
                        }
                    }
                }
            }
        });

        return new ExportMetricsServiceRequest
        {
            ResourceMetrics =
            {
                new ResourceMetrics
                {
                    SchemaUrl = "https://opentelemetry.io/schemas/1.38.0",
                    Resource = new Resource
                    {
                        DroppedAttributesCount = 2,
                        Attributes =
                        {
                            StringAttribute("service.name", "metric-service"),
                            StringAttribute("service.namespace", "tests")
                        }
                    },
                    ScopeMetrics = { scopeMetrics }
                }
            }
        };
    }

    private static KeyValue StringAttribute(string key, string value) =>
        new() { Key = key, Value = new AnyValue { StringValue = value } };
}
