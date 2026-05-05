
#nullable disable

using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Text.Json;
using Qyl.Client;

namespace Qyl.Api
{
    public partial class MetricQueryResponse : IJsonModel<MetricQueryResponse>
    {
        internal MetricQueryResponse()
        {
        }

        protected virtual MetricQueryResponse PersistableModelCreateCore(BinaryData data, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<MetricQueryResponse>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    using (JsonDocument document = JsonDocument.Parse(data, ModelSerializationExtensions.JsonDocumentOptions))
                    {
                        return DeserializeMetricQueryResponse(document.RootElement, options);
                    }
                default:
                    throw new FormatException($"The model {nameof(MetricQueryResponse)} does not support reading '{options.Format}' format.");
            }
        }

        protected virtual BinaryData PersistableModelWriteCore(ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<MetricQueryResponse>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    return ModelReaderWriter.Write(this, options, QylClientContext.Default);
                default:
                    throw new FormatException($"The model {nameof(MetricQueryResponse)} does not support writing '{options.Format}' format.");
            }
        }

        BinaryData IPersistableModel<MetricQueryResponse>.Write(ModelReaderWriterOptions options) => PersistableModelWriteCore(options);

        MetricQueryResponse IPersistableModel<MetricQueryResponse>.Create(BinaryData data, ModelReaderWriterOptions options) => PersistableModelCreateCore(data, options);

        string IPersistableModel<MetricQueryResponse>.GetFormatFromOptions(ModelReaderWriterOptions options) => "J";

        public static explicit operator MetricQueryResponse(ClientResult result)
        {
            PipelineResponse response = result.GetRawResponse();
            using JsonDocument document = JsonDocument.Parse(response.Content, ModelSerializationExtensions.JsonDocumentOptions);
            return DeserializeMetricQueryResponse(document.RootElement, ModelSerializationExtensions.WireOptions);
        }

        void IJsonModel<MetricQueryResponse>.Write(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            writer.WriteStartObject();
            JsonModelWriteCore(writer, options);
            writer.WriteEndObject();
        }

        protected virtual void JsonModelWriteCore(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<MetricQueryResponse>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(MetricQueryResponse)} does not support writing '{format}' format.");
            }
            writer.WritePropertyName("metric_name"u8);
            writer.WriteStringValue(MetricName);
            writer.WritePropertyName("series"u8);
            writer.WriteStartArray();
            foreach (MetricTimeSeries item in Series)
            {
                writer.WriteObjectValue(item, options);
            }
            writer.WriteEndArray();
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

        MetricQueryResponse IJsonModel<MetricQueryResponse>.Create(ref Utf8JsonReader reader, ModelReaderWriterOptions options) => JsonModelCreateCore(ref reader, options);

        protected virtual MetricQueryResponse JsonModelCreateCore(ref Utf8JsonReader reader, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<MetricQueryResponse>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(MetricQueryResponse)} does not support reading '{format}' format.");
            }
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            return DeserializeMetricQueryResponse(document.RootElement, options);
        }

        internal static MetricQueryResponse DeserializeMetricQueryResponse(JsonElement element, ModelReaderWriterOptions options)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }
            string metricName = default;
            IList<MetricTimeSeries> series = default;
            IDictionary<string, BinaryData> additionalBinaryDataProperties = new ChangeTrackingDictionary<string, BinaryData>();
            foreach (var prop in element.EnumerateObject())
            {
                if (prop.NameEquals("metric_name"u8))
                {
                    metricName = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("series"u8))
                {
                    List<MetricTimeSeries> array = new List<MetricTimeSeries>();
                    foreach (var item in prop.Value.EnumerateArray())
                    {
                        array.Add(MetricTimeSeries.DeserializeMetricTimeSeries(item, options));
                    }
                    series = array;
                    continue;
                }
                if (options.Format != "W")
                {
                    additionalBinaryDataProperties.Add(prop.Name, BinaryData.FromString(prop.Value.GetRawText()));
                }
            }
            return new MetricQueryResponse(metricName, series, additionalBinaryDataProperties);
        }
    }
}
