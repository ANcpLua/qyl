
#nullable disable

using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Text.Json;
using Qyl.Client;
using Qyl.Common.Pagination;
using Qyl.OTel.Metrics;

namespace Qyl.Api
{
    public partial class MetricQueryRequest : IJsonModel<MetricQueryRequest>
    {
        internal MetricQueryRequest()
        {
        }

        protected virtual MetricQueryRequest PersistableModelCreateCore(BinaryData data, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<MetricQueryRequest>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    using (JsonDocument document = JsonDocument.Parse(data, ModelSerializationExtensions.JsonDocumentOptions))
                    {
                        return DeserializeMetricQueryRequest(document.RootElement, options);
                    }
                default:
                    throw new FormatException($"The model {nameof(MetricQueryRequest)} does not support reading '{options.Format}' format.");
            }
        }

        protected virtual BinaryData PersistableModelWriteCore(ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<MetricQueryRequest>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    return ModelReaderWriter.Write(this, options, QylClientContext.Default);
                default:
                    throw new FormatException($"The model {nameof(MetricQueryRequest)} does not support writing '{options.Format}' format.");
            }
        }

        BinaryData IPersistableModel<MetricQueryRequest>.Write(ModelReaderWriterOptions options) => PersistableModelWriteCore(options);

        MetricQueryRequest IPersistableModel<MetricQueryRequest>.Create(BinaryData data, ModelReaderWriterOptions options) => PersistableModelCreateCore(data, options);

        string IPersistableModel<MetricQueryRequest>.GetFormatFromOptions(ModelReaderWriterOptions options) => "J";

        public static implicit operator BinaryContent(MetricQueryRequest metricQueryRequest)
        {
            if (metricQueryRequest == null)
            {
                return null;
            }
            return BinaryContent.Create(metricQueryRequest, ModelSerializationExtensions.WireOptions);
        }

        void IJsonModel<MetricQueryRequest>.Write(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            writer.WriteStartObject();
            JsonModelWriteCore(writer, options);
            writer.WriteEndObject();
        }

        protected virtual void JsonModelWriteCore(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<MetricQueryRequest>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(MetricQueryRequest)} does not support writing '{format}' format.");
            }
            writer.WritePropertyName("metric_name"u8);
            writer.WriteStringValue(MetricName);
            if (Optional.IsCollectionDefined(Filters))
            {
                writer.WritePropertyName("filters"u8);
                writer.WriteStartObject();
                foreach (var item in Filters)
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
            }
            writer.WritePropertyName("start_time"u8);
            writer.WriteStringValue(StartTime, "O");
            writer.WritePropertyName("end_time"u8);
            writer.WriteStringValue(EndTime, "O");
            if (Optional.IsDefined(Step))
            {
                writer.WritePropertyName("step"u8);
                writer.WriteStringValue(Step.Value.ToSerialString());
            }
            if (Optional.IsDefined(Aggregation))
            {
                writer.WritePropertyName("aggregation"u8);
                writer.WriteStringValue(Aggregation.Value.ToSerialString());
            }
            if (Optional.IsCollectionDefined(GroupBy))
            {
                writer.WritePropertyName("group_by"u8);
                writer.WriteStartArray();
                foreach (string item in GroupBy)
                {
                    if (item == null)
                    {
                        writer.WriteNullValue();
                        continue;
                    }
                    writer.WriteStringValue(item);
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

        MetricQueryRequest IJsonModel<MetricQueryRequest>.Create(ref Utf8JsonReader reader, ModelReaderWriterOptions options) => JsonModelCreateCore(ref reader, options);

        protected virtual MetricQueryRequest JsonModelCreateCore(ref Utf8JsonReader reader, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<MetricQueryRequest>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(MetricQueryRequest)} does not support reading '{format}' format.");
            }
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            return DeserializeMetricQueryRequest(document.RootElement, options);
        }

        internal static MetricQueryRequest DeserializeMetricQueryRequest(JsonElement element, ModelReaderWriterOptions options)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }
            string metricName = default;
            IDictionary<string, string> filters = default;
            DateTimeOffset startTime = default;
            DateTimeOffset endTime = default;
            TimeBucket? step = default;
            AggregationFunction? aggregation = default;
            IList<string> groupBy = default;
            IDictionary<string, BinaryData> additionalBinaryDataProperties = new ChangeTrackingDictionary<string, BinaryData>();
            foreach (var prop in element.EnumerateObject())
            {
                if (prop.NameEquals("metric_name"u8))
                {
                    metricName = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("filters"u8))
                {
                    if (prop.Value.ValueKind == JsonValueKind.Null)
                    {
                        continue;
                    }
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
                    filters = dictionary;
                    continue;
                }
                if (prop.NameEquals("start_time"u8))
                {
                    startTime = prop.Value.GetDateTimeOffset("O");
                    continue;
                }
                if (prop.NameEquals("end_time"u8))
                {
                    endTime = prop.Value.GetDateTimeOffset("O");
                    continue;
                }
                if (prop.NameEquals("step"u8))
                {
                    if (prop.Value.ValueKind == JsonValueKind.Null)
                    {
                        continue;
                    }
                    step = prop.Value.GetString().ToTimeBucket();
                    continue;
                }
                if (prop.NameEquals("aggregation"u8))
                {
                    if (prop.Value.ValueKind == JsonValueKind.Null)
                    {
                        continue;
                    }
                    aggregation = prop.Value.GetString().ToAggregationFunction();
                    continue;
                }
                if (prop.NameEquals("group_by"u8))
                {
                    if (prop.Value.ValueKind == JsonValueKind.Null)
                    {
                        continue;
                    }
                    List<string> array = new List<string>();
                    foreach (var item in prop.Value.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.Null)
                        {
                            array.Add(null);
                        }
                        else
                        {
                            array.Add(item.GetString());
                        }
                    }
                    groupBy = array;
                    continue;
                }
                if (options.Format != "W")
                {
                    additionalBinaryDataProperties.Add(prop.Name, BinaryData.FromString(prop.Value.GetRawText()));
                }
            }
            return new MetricQueryRequest(
                metricName,
                filters ?? new ChangeTrackingDictionary<string, string>(),
                startTime,
                endTime,
                step,
                aggregation,
                groupBy ?? new ChangeTrackingList<string>(),
                additionalBinaryDataProperties);
        }
    }
}
