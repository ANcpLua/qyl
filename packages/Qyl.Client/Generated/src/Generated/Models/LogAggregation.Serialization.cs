
#nullable disable

using System;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Text.Json;
using Qyl.Client;
using Qyl.OTel.Metrics;
using Qyl.Common.Pagination;

namespace Qyl.Domains.Observe.Log
{
    public partial class LogAggregation : IJsonModel<LogAggregation>
    {
        internal LogAggregation()
        {
        }

        protected virtual LogAggregation PersistableModelCreateCore(BinaryData data, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<LogAggregation>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    using (JsonDocument document = JsonDocument.Parse(data, ModelSerializationExtensions.JsonDocumentOptions))
                    {
                        return DeserializeLogAggregation(document.RootElement, options);
                    }
                default:
                    throw new FormatException($"The model {nameof(LogAggregation)} does not support reading '{options.Format}' format.");
            }
        }

        protected virtual BinaryData PersistableModelWriteCore(ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<LogAggregation>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    return ModelReaderWriter.Write(this, options, QylClientContext.Default);
                default:
                    throw new FormatException($"The model {nameof(LogAggregation)} does not support writing '{options.Format}' format.");
            }
        }

        BinaryData IPersistableModel<LogAggregation>.Write(ModelReaderWriterOptions options) => PersistableModelWriteCore(options);

        LogAggregation IPersistableModel<LogAggregation>.Create(BinaryData data, ModelReaderWriterOptions options) => PersistableModelCreateCore(data, options);

        string IPersistableModel<LogAggregation>.GetFormatFromOptions(ModelReaderWriterOptions options) => "J";

        void IJsonModel<LogAggregation>.Write(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            writer.WriteStartObject();
            JsonModelWriteCore(writer, options);
            writer.WriteEndObject();
        }

        protected virtual void JsonModelWriteCore(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<LogAggregation>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(LogAggregation)} does not support writing '{format}' format.");
            }
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
            writer.WritePropertyName("function"u8);
            writer.WriteStringValue(Function.ToSerialString());
            if (Optional.IsDefined(Field))
            {
                writer.WritePropertyName("field"u8);
                writer.WriteStringValue(Field);
            }
            if (Optional.IsDefined(TimeBucket))
            {
                writer.WritePropertyName("time_bucket"u8);
                writer.WriteStringValue(TimeBucket.Value.ToSerialString());
            }
            if (Optional.IsDefined(TopN))
            {
                writer.WritePropertyName("top_n"u8);
                writer.WriteNumberValue(TopN.Value);
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

        LogAggregation IJsonModel<LogAggregation>.Create(ref Utf8JsonReader reader, ModelReaderWriterOptions options) => JsonModelCreateCore(ref reader, options);

        protected virtual LogAggregation JsonModelCreateCore(ref Utf8JsonReader reader, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<LogAggregation>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(LogAggregation)} does not support reading '{format}' format.");
            }
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            return DeserializeLogAggregation(document.RootElement, options);
        }

        internal static LogAggregation DeserializeLogAggregation(JsonElement element, ModelReaderWriterOptions options)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }
            IList<string> groupBy = default;
            AggregationFunction function = default;
            string @field = default;
            TimeBucket? timeBucket = default;
            int? topN = default;
            IDictionary<string, BinaryData> additionalBinaryDataProperties = new ChangeTrackingDictionary<string, BinaryData>();
            foreach (var prop in element.EnumerateObject())
            {
                if (prop.NameEquals("group_by"u8))
                {
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
                if (prop.NameEquals("function"u8))
                {
                    function = prop.Value.GetString().ToAggregationFunction();
                    continue;
                }
                if (prop.NameEquals("field"u8))
                {
                    @field = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("time_bucket"u8))
                {
                    if (prop.Value.ValueKind == JsonValueKind.Null)
                    {
                        continue;
                    }
                    timeBucket = prop.Value.GetString().ToTimeBucket();
                    continue;
                }
                if (prop.NameEquals("top_n"u8))
                {
                    if (prop.Value.ValueKind == JsonValueKind.Null)
                    {
                        continue;
                    }
                    topN = prop.Value.GetInt32();
                    continue;
                }
                if (options.Format != "W")
                {
                    additionalBinaryDataProperties.Add(prop.Name, BinaryData.FromString(prop.Value.GetRawText()));
                }
            }
            return new LogAggregation(
                groupBy,
                function,
                @field,
                timeBucket,
                topN,
                additionalBinaryDataProperties);
        }
    }
}
