
#nullable disable

using System;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Text.Json;
using Qyl.Client;

namespace Qyl.Domains.Observe.Log
{
    public partial class LogPattern : IJsonModel<LogPattern>
    {
        internal LogPattern()
        {
        }

        protected virtual LogPattern PersistableModelCreateCore(BinaryData data, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<LogPattern>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    using (JsonDocument document = JsonDocument.Parse(data, ModelSerializationExtensions.JsonDocumentOptions))
                    {
                        return DeserializeLogPattern(document.RootElement, options);
                    }
                default:
                    throw new FormatException($"The model {nameof(LogPattern)} does not support reading '{options.Format}' format.");
            }
        }

        protected virtual BinaryData PersistableModelWriteCore(ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<LogPattern>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    return ModelReaderWriter.Write(this, options, QylClientContext.Default);
                default:
                    throw new FormatException($"The model {nameof(LogPattern)} does not support writing '{options.Format}' format.");
            }
        }

        BinaryData IPersistableModel<LogPattern>.Write(ModelReaderWriterOptions options) => PersistableModelWriteCore(options);

        LogPattern IPersistableModel<LogPattern>.Create(BinaryData data, ModelReaderWriterOptions options) => PersistableModelCreateCore(data, options);

        string IPersistableModel<LogPattern>.GetFormatFromOptions(ModelReaderWriterOptions options) => "J";

        void IJsonModel<LogPattern>.Write(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            writer.WriteStartObject();
            JsonModelWriteCore(writer, options);
            writer.WriteEndObject();
        }

        protected virtual void JsonModelWriteCore(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<LogPattern>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(LogPattern)} does not support writing '{format}' format.");
            }
            writer.WritePropertyName("pattern_id"u8);
            writer.WriteStringValue(PatternId);
            writer.WritePropertyName("template"u8);
            writer.WriteStringValue(Template);
            writer.WritePropertyName("sample"u8);
            writer.WriteStringValue(Sample);
            writer.WritePropertyName("count"u8);
            writer.WriteNumberValue(Count);
            writer.WritePropertyName("first_seen"u8);
            writer.WriteStringValue(FirstSeen, "O");
            writer.WritePropertyName("last_seen"u8);
            writer.WriteStringValue(LastSeen, "O");
            writer.WritePropertyName("trend"u8);
            writer.WriteStringValue(Trend.ToSerialString());
            if (Optional.IsCollectionDefined(SeverityDistribution))
            {
                writer.WritePropertyName("severity_distribution"u8);
                writer.WriteStartArray();
                foreach (LogSeverityStats item in SeverityDistribution)
                {
                    writer.WriteObjectValue(item, options);
                }
                writer.WriteEndArray();
            }
            if (options.Format != "W" && _additionalBinaryDataProperties != null)
            {
                foreach (var item in _additionalBinaryDataProperties)
                {
                    writer.WritePropertyName(item.Key);
#if NET6_0_OR_GREATER
                    writer.WriteRawValue(item.Value);
#else
                    using (JsonDocument document = JsonDocument.Parse(item.Value))
                    {
                        JsonSerializer.Serialize(writer, document.RootElement);
                    }
#endif
                }
            }
        }

        LogPattern IJsonModel<LogPattern>.Create(ref Utf8JsonReader reader, ModelReaderWriterOptions options) => JsonModelCreateCore(ref reader, options);

        protected virtual LogPattern JsonModelCreateCore(ref Utf8JsonReader reader, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<LogPattern>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(LogPattern)} does not support reading '{format}' format.");
            }
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            return DeserializeLogPattern(document.RootElement, options);
        }

        internal static LogPattern DeserializeLogPattern(JsonElement element, ModelReaderWriterOptions options)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }
            string patternId = default;
            string template = default;
            string sample = default;
            long count = default;
            DateTimeOffset firstSeen = default;
            DateTimeOffset lastSeen = default;
            LogPatternTrend trend = default;
            IList<LogSeverityStats> severityDistribution = default;
            IDictionary<string, BinaryData> additionalBinaryDataProperties = new ChangeTrackingDictionary<string, BinaryData>();
            foreach (var prop in element.EnumerateObject())
            {
                if (prop.NameEquals("pattern_id"u8))
                {
                    patternId = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("template"u8))
                {
                    template = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("sample"u8))
                {
                    sample = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("count"u8))
                {
                    count = prop.Value.GetInt64();
                    continue;
                }
                if (prop.NameEquals("first_seen"u8))
                {
                    firstSeen = prop.Value.GetDateTimeOffset("O");
                    continue;
                }
                if (prop.NameEquals("last_seen"u8))
                {
                    lastSeen = prop.Value.GetDateTimeOffset("O");
                    continue;
                }
                if (prop.NameEquals("trend"u8))
                {
                    trend = prop.Value.GetString().ToLogPatternTrend();
                    continue;
                }
                if (prop.NameEquals("severity_distribution"u8))
                {
                    if (prop.Value.ValueKind == JsonValueKind.Null)
                    {
                        continue;
                    }
                    List<LogSeverityStats> array = new List<LogSeverityStats>();
                    foreach (var item in prop.Value.EnumerateArray())
                    {
                        array.Add(LogSeverityStats.DeserializeLogSeverityStats(item, options));
                    }
                    severityDistribution = array;
                    continue;
                }
                if (options.Format != "W")
                {
                    additionalBinaryDataProperties.Add(prop.Name, BinaryData.FromString(prop.Value.GetRawText()));
                }
            }
            return new LogPattern(
                patternId,
                template,
                sample,
                count,
                firstSeen,
                lastSeen,
                trend,
                severityDistribution ?? new ChangeTrackingList<LogSeverityStats>(),
                additionalBinaryDataProperties);
        }
    }
}
