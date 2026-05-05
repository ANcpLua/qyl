
#nullable disable

using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Text.Json;
using Qyl.Client;

namespace Qyl.OTel.Logs
{
    public partial class LogStats : IJsonModel<LogStats>
    {
        internal LogStats()
        {
        }

        protected virtual LogStats PersistableModelCreateCore(BinaryData data, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<LogStats>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    using (JsonDocument document = JsonDocument.Parse(data, ModelSerializationExtensions.JsonDocumentOptions))
                    {
                        return DeserializeLogStats(document.RootElement, options);
                    }
                default:
                    throw new FormatException($"The model {nameof(LogStats)} does not support reading '{options.Format}' format.");
            }
        }

        protected virtual BinaryData PersistableModelWriteCore(ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<LogStats>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    return ModelReaderWriter.Write(this, options, QylClientContext.Default);
                default:
                    throw new FormatException($"The model {nameof(LogStats)} does not support writing '{options.Format}' format.");
            }
        }

        BinaryData IPersistableModel<LogStats>.Write(ModelReaderWriterOptions options) => PersistableModelWriteCore(options);

        LogStats IPersistableModel<LogStats>.Create(BinaryData data, ModelReaderWriterOptions options) => PersistableModelCreateCore(data, options);

        string IPersistableModel<LogStats>.GetFormatFromOptions(ModelReaderWriterOptions options) => "J";

        public static explicit operator LogStats(ClientResult result)
        {
            PipelineResponse response = result.GetRawResponse();
            using JsonDocument document = JsonDocument.Parse(response.Content, ModelSerializationExtensions.JsonDocumentOptions);
            return DeserializeLogStats(document.RootElement, ModelSerializationExtensions.WireOptions);
        }

        void IJsonModel<LogStats>.Write(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            writer.WriteStartObject();
            JsonModelWriteCore(writer, options);
            writer.WriteEndObject();
        }

        protected virtual void JsonModelWriteCore(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<LogStats>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(LogStats)} does not support writing '{format}' format.");
            }
            writer.WritePropertyName("total_count"u8);
            writer.WriteNumberValue(TotalCount);
            writer.WritePropertyName("by_severity"u8);
            writer.WriteStartArray();
            foreach (LogCountBySeverity item in BySeverity)
            {
                writer.WriteObjectValue(item, options);
            }
            writer.WriteEndArray();
            writer.WritePropertyName("by_service"u8);
            writer.WriteStartArray();
            foreach (LogCountByDimension item in ByService)
            {
                writer.WriteObjectValue(item, options);
            }
            writer.WriteEndArray();
            writer.WritePropertyName("logs_per_second"u8);
            writer.WriteNumberValue(LogsPerSecond);
            writer.WritePropertyName("error_rate"u8);
            writer.WriteNumberValue(ErrorRate);
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

        LogStats IJsonModel<LogStats>.Create(ref Utf8JsonReader reader, ModelReaderWriterOptions options) => JsonModelCreateCore(ref reader, options);

        protected virtual LogStats JsonModelCreateCore(ref Utf8JsonReader reader, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<LogStats>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(LogStats)} does not support reading '{format}' format.");
            }
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            return DeserializeLogStats(document.RootElement, options);
        }

        internal static LogStats DeserializeLogStats(JsonElement element, ModelReaderWriterOptions options)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }
            long totalCount = default;
            IList<LogCountBySeverity> bySeverity = default;
            IList<LogCountByDimension> byService = default;
            double logsPerSecond = default;
            double errorRate = default;
            IDictionary<string, BinaryData> additionalBinaryDataProperties = new ChangeTrackingDictionary<string, BinaryData>();
            foreach (var prop in element.EnumerateObject())
            {
                if (prop.NameEquals("total_count"u8))
                {
                    totalCount = prop.Value.GetInt64();
                    continue;
                }
                if (prop.NameEquals("by_severity"u8))
                {
                    List<LogCountBySeverity> array = new List<LogCountBySeverity>();
                    foreach (var item in prop.Value.EnumerateArray())
                    {
                        array.Add(LogCountBySeverity.DeserializeLogCountBySeverity(item, options));
                    }
                    bySeverity = array;
                    continue;
                }
                if (prop.NameEquals("by_service"u8))
                {
                    List<LogCountByDimension> array = new List<LogCountByDimension>();
                    foreach (var item in prop.Value.EnumerateArray())
                    {
                        array.Add(LogCountByDimension.DeserializeLogCountByDimension(item, options));
                    }
                    byService = array;
                    continue;
                }
                if (prop.NameEquals("logs_per_second"u8))
                {
                    logsPerSecond = prop.Value.GetDouble();
                    continue;
                }
                if (prop.NameEquals("error_rate"u8))
                {
                    errorRate = prop.Value.GetDouble();
                    continue;
                }
                if (options.Format != "W")
                {
                    additionalBinaryDataProperties.Add(prop.Name, BinaryData.FromString(prop.Value.GetRawText()));
                }
            }
            return new LogStats(
                totalCount,
                bySeverity,
                byService,
                logsPerSecond,
                errorRate,
                additionalBinaryDataProperties);
        }
    }
}
