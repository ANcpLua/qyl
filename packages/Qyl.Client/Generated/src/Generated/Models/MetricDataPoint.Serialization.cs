
#nullable disable

using System;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Text.Json;
using Qyl.Client;

namespace Qyl.Api
{
    public partial class MetricDataPoint : IJsonModel<MetricDataPoint>
    {
        internal MetricDataPoint()
        {
        }

        protected virtual MetricDataPoint PersistableModelCreateCore(BinaryData data, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<MetricDataPoint>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    using (JsonDocument document = JsonDocument.Parse(data, ModelSerializationExtensions.JsonDocumentOptions))
                    {
                        return DeserializeMetricDataPoint(document.RootElement, options);
                    }
                default:
                    throw new FormatException($"The model {nameof(MetricDataPoint)} does not support reading '{options.Format}' format.");
            }
        }

        protected virtual BinaryData PersistableModelWriteCore(ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<MetricDataPoint>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    return ModelReaderWriter.Write(this, options, QylClientContext.Default);
                default:
                    throw new FormatException($"The model {nameof(MetricDataPoint)} does not support writing '{options.Format}' format.");
            }
        }

        BinaryData IPersistableModel<MetricDataPoint>.Write(ModelReaderWriterOptions options) => PersistableModelWriteCore(options);

        MetricDataPoint IPersistableModel<MetricDataPoint>.Create(BinaryData data, ModelReaderWriterOptions options) => PersistableModelCreateCore(data, options);

        string IPersistableModel<MetricDataPoint>.GetFormatFromOptions(ModelReaderWriterOptions options) => "J";

        void IJsonModel<MetricDataPoint>.Write(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            writer.WriteStartObject();
            JsonModelWriteCore(writer, options);
            writer.WriteEndObject();
        }

        protected virtual void JsonModelWriteCore(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<MetricDataPoint>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(MetricDataPoint)} does not support writing '{format}' format.");
            }
            writer.WritePropertyName("timestamp"u8);
            writer.WriteStringValue(Timestamp, "O");
            writer.WritePropertyName("value"u8);
            writer.WriteNumberValue(Value);
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

        MetricDataPoint IJsonModel<MetricDataPoint>.Create(ref Utf8JsonReader reader, ModelReaderWriterOptions options) => JsonModelCreateCore(ref reader, options);

        protected virtual MetricDataPoint JsonModelCreateCore(ref Utf8JsonReader reader, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<MetricDataPoint>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(MetricDataPoint)} does not support reading '{format}' format.");
            }
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            return DeserializeMetricDataPoint(document.RootElement, options);
        }

        internal static MetricDataPoint DeserializeMetricDataPoint(JsonElement element, ModelReaderWriterOptions options)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }
            DateTimeOffset timestamp = default;
            double value = default;
            IDictionary<string, BinaryData> additionalBinaryDataProperties = new ChangeTrackingDictionary<string, BinaryData>();
            foreach (var prop in element.EnumerateObject())
            {
                if (prop.NameEquals("timestamp"u8))
                {
                    timestamp = prop.Value.GetDateTimeOffset("O");
                    continue;
                }
                if (prop.NameEquals("value"u8))
                {
                    value = prop.Value.GetDouble();
                    continue;
                }
                if (options.Format != "W")
                {
                    additionalBinaryDataProperties.Add(prop.Name, BinaryData.FromString(prop.Value.GetRawText()));
                }
            }
            return new MetricDataPoint(timestamp, value, additionalBinaryDataProperties);
        }
    }
}
