
#nullable disable

using System;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Text.Json;
using Qyl.Client;

namespace Qyl.Api
{
    public partial class MetricTimeSeries : IJsonModel<MetricTimeSeries>
    {
        internal MetricTimeSeries()
        {
        }

        protected virtual MetricTimeSeries PersistableModelCreateCore(BinaryData data, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<MetricTimeSeries>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    using (JsonDocument document = JsonDocument.Parse(data, ModelSerializationExtensions.JsonDocumentOptions))
                    {
                        return DeserializeMetricTimeSeries(document.RootElement, options);
                    }
                default:
                    throw new FormatException($"The model {nameof(MetricTimeSeries)} does not support reading '{options.Format}' format.");
            }
        }

        protected virtual BinaryData PersistableModelWriteCore(ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<MetricTimeSeries>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    return ModelReaderWriter.Write(this, options, QylClientContext.Default);
                default:
                    throw new FormatException($"The model {nameof(MetricTimeSeries)} does not support writing '{options.Format}' format.");
            }
        }

        BinaryData IPersistableModel<MetricTimeSeries>.Write(ModelReaderWriterOptions options) => PersistableModelWriteCore(options);

        MetricTimeSeries IPersistableModel<MetricTimeSeries>.Create(BinaryData data, ModelReaderWriterOptions options) => PersistableModelCreateCore(data, options);

        string IPersistableModel<MetricTimeSeries>.GetFormatFromOptions(ModelReaderWriterOptions options) => "J";

        void IJsonModel<MetricTimeSeries>.Write(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            writer.WriteStartObject();
            JsonModelWriteCore(writer, options);
            writer.WriteEndObject();
        }

        protected virtual void JsonModelWriteCore(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<MetricTimeSeries>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(MetricTimeSeries)} does not support writing '{format}' format.");
            }
            writer.WritePropertyName("labels"u8);
            writer.WriteStartObject();
            foreach (var item in Labels)
            {
                writer.WritePropertyName(item.Key);
                if (item.Value == null)
                {
                    writer.WriteNullValue();
                    continue;
                }
                writer.WriteStringValue(item.Value);
            }
            writer.WriteEndObject();
            writer.WritePropertyName("points"u8);
            writer.WriteStartArray();
            foreach (MetricDataPoint item in Points)
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

        MetricTimeSeries IJsonModel<MetricTimeSeries>.Create(ref Utf8JsonReader reader, ModelReaderWriterOptions options) => JsonModelCreateCore(ref reader, options);

        protected virtual MetricTimeSeries JsonModelCreateCore(ref Utf8JsonReader reader, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<MetricTimeSeries>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(MetricTimeSeries)} does not support reading '{format}' format.");
            }
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            return DeserializeMetricTimeSeries(document.RootElement, options);
        }

        internal static MetricTimeSeries DeserializeMetricTimeSeries(JsonElement element, ModelReaderWriterOptions options)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }
            IDictionary<string, string> labels = default;
            IList<MetricDataPoint> points = default;
            IDictionary<string, BinaryData> additionalBinaryDataProperties = new ChangeTrackingDictionary<string, BinaryData>();
            foreach (var prop in element.EnumerateObject())
            {
                if (prop.NameEquals("labels"u8))
                {
                    Dictionary<string, string> dictionary = new Dictionary<string, string>();
                    foreach (var prop0 in prop.Value.EnumerateObject())
                    {
                        if (prop0.Value.ValueKind == JsonValueKind.Null)
                        {
                            dictionary.Add(prop0.Name, null);
                        }
                        else
                        {
                            dictionary.Add(prop0.Name, prop0.Value.GetString());
                        }
                    }
                    labels = dictionary;
                    continue;
                }
                if (prop.NameEquals("points"u8))
                {
                    List<MetricDataPoint> array = new List<MetricDataPoint>();
                    foreach (var item in prop.Value.EnumerateArray())
                    {
                        array.Add(MetricDataPoint.DeserializeMetricDataPoint(item, options));
                    }
                    points = array;
                    continue;
                }
                if (options.Format != "W")
                {
                    additionalBinaryDataProperties.Add(prop.Name, BinaryData.FromString(prop.Value.GetRawText()));
                }
            }
            return new MetricTimeSeries(labels, points, additionalBinaryDataProperties);
        }
    }
}
