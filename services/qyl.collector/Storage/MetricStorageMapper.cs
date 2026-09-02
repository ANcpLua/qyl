using System.Text;

namespace Qyl.Collector.Storage;

/// <summary>
/// Folds the flat OTLP data points the converter produces into the two-table storage shape:
/// one <see cref="MetricSeriesRow" /> per distinct stream and one <see cref="MetricPointRow" />
/// per sample.
/// </summary>
internal static class MetricStorageMapper
{
    internal readonly record struct MetricWriteSet(
        IReadOnlyList<MetricSeriesRow> Series,
        IReadOnlyList<MetricPointRow> Points);

    public static MetricWriteSet ToStorageRows(MetricIngestionBatch batch)
    {
        var series = new Dictionary<(string ProjectId, string SeriesId), MetricSeriesRow>();
        var points = new Dictionary<(string ProjectId, string SeriesId, ulong Time), MetricPointRow>();

        foreach (var record in batch.Points)
        {
            var projectId = ProjectScope.Normalize(record.ProjectIdHint);
            var attributesJson = PersistedAttributePolicy.SerializeMetricAttributes(record.Attributes);
            var resourceJson = PersistedAttributePolicy.SerializeResourceAttributes(
                record.ResourceAttributes,
                record.ResourceEntityRefs);
            var resourceEntityRefsJson = SerializeResourceEntityRefs(record.ResourceEntityRefs);
            var seriesId = ComputeSeriesId(
                record.MetricName,
                record.ServiceName,
                attributesJson,
                resourceJson);

            var key = (projectId, seriesId);
            var row = new MetricSeriesRow
            {
                ProjectId = projectId,
                SeriesId = seriesId,
                MetricName = record.MetricName,
                MetricKind = (byte)record.Kind,
                Temporality = (byte)record.Temporality,
                IsMonotonic = record.IsMonotonic ? (byte)1 : (byte)0,
                Unit = record.Unit,
                Description = record.Description,
                ServiceName = record.ServiceName,
                SchemaUrl = record.SchemaUrl,
                AttributesJson = attributesJson,
                ResourceJson = resourceJson,
                ResourceEntityRefsJson = resourceEntityRefsJson,
                FirstSeenUnixNano = record.TimeUnixNano,
                LastSeenUnixNano = record.TimeUnixNano
            };

            // One export can carry many points for one stream. Collapse them here rather than
            // sending N conflicting inserts through the writer for the same primary key.
            series[key] = series.TryGetValue(key, out var existing)
                ? row with
                {
                    FirstSeenUnixNano = Math.Min(existing.FirstSeenUnixNano, record.TimeUnixNano),
                    LastSeenUnixNano = Math.Max(existing.LastSeenUnixNano, record.TimeUnixNano)
                }
                : row;

            points[(projectId, seriesId, record.TimeUnixNano)] = new MetricPointRow
            {
                ProjectId = projectId,
                SeriesId = seriesId,
                TimeUnixNano = record.TimeUnixNano,
                StartTimeUnixNano = record.StartTimeUnixNano,
                Value = record.Value,
                HistogramCount = record.Count,
                HistogramSum = record.Sum,
                HistogramMin = record.Min,
                HistogramMax = record.Max,
                BucketBoundsJson = SerializeDoubles(record.BucketBounds),
                BucketCountsJson = SerializeCounts(record.BucketCounts)
            };
        }

        return new MetricWriteSet([.. series.Values], [.. points.Values]);
    }

    /// <summary>
    /// Series identity is metric name plus everything that distinguishes one stream of it from
    /// another: the persisted point attributes, the service, and the resource projection. It
    /// deliberately excludes unit, description, temporality and monotonicity — those describe
    /// the instrument, and an SDK correcting one of them must update the series, not fork it.
    /// </summary>
    internal static string ComputeSeriesId(
        string metricName,
        string? serviceName,
        string? attributesJson,
        string? resourceJson)
    {
        var builder = new StringBuilder(512);
        builder.Append("metric_series\n");
        AppendIdentityPart(builder, metricName);
        AppendIdentityPart(builder, serviceName);
        AppendIdentityPart(builder, attributesJson);
        AppendIdentityPart(builder, resourceJson);

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return "ms_" + Convert.ToHexString(hash.AsSpan(0, 16)).ToLowerInvariant();
    }

    private static void AppendIdentityPart(StringBuilder builder, string? value)
    {
        if (value is null)
        {
            builder.Append("-1:\n");
            return;
        }

        builder
            .Append(value.Length.ToString(CultureInfo.InvariantCulture))
            .Append(':')
            .Append(value)
            .Append('\n');
    }

    private static string? SerializeDoubles(IReadOnlyList<double>? values)
    {
        if (values is null || values.Count is 0)
            return null;

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartArray();
            foreach (var value in values)
                writer.WriteNumberValue(value);
            writer.WriteEndArray();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static string? SerializeCounts(IReadOnlyList<ulong>? values)
    {
        if (values is null || values.Count is 0)
            return null;

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartArray();
            foreach (var value in values)
                writer.WriteNumberValue(value);
            writer.WriteEndArray();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static string? SerializeResourceEntityRefs(
        IReadOnlyList<ResourceEntityRefIngestionRecord> entityRefs) =>
        entityRefs.Count is 0
            ? null
            : JsonSerializer.Serialize(
                entityRefs as List<ResourceEntityRefIngestionRecord> ?? [.. entityRefs],
                StorageJsonSerializerContext.Default.ResourceEntityRefIngestionRecordList);
}
