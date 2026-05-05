
#nullable disable

using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Text.Json;
using Qyl.Client;

namespace Qyl.Api
{
    public partial class LogAggregationResponse : IJsonModel<LogAggregationResponse>
    {
        internal LogAggregationResponse()
        {
        }

        protected virtual LogAggregationResponse PersistableModelCreateCore(BinaryData data, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<LogAggregationResponse>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    using (JsonDocument document = JsonDocument.Parse(data, ModelSerializationExtensions.JsonDocumentOptions))
                    {
                        return DeserializeLogAggregationResponse(document.RootElement, options);
                    }
                default:
                    throw new FormatException($"The model {nameof(LogAggregationResponse)} does not support reading '{options.Format}' format.");
            }
        }

        protected virtual BinaryData PersistableModelWriteCore(ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<LogAggregationResponse>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    return ModelReaderWriter.Write(this, options, QylClientContext.Default);
                default:
                    throw new FormatException($"The model {nameof(LogAggregationResponse)} does not support writing '{options.Format}' format.");
            }
        }

        BinaryData IPersistableModel<LogAggregationResponse>.Write(ModelReaderWriterOptions options) => PersistableModelWriteCore(options);

        LogAggregationResponse IPersistableModel<LogAggregationResponse>.Create(BinaryData data, ModelReaderWriterOptions options) => PersistableModelCreateCore(data, options);

        string IPersistableModel<LogAggregationResponse>.GetFormatFromOptions(ModelReaderWriterOptions options) => "J";

        public static explicit operator LogAggregationResponse(ClientResult result)
        {
            PipelineResponse response = result.GetRawResponse();
            using JsonDocument document = JsonDocument.Parse(response.Content, ModelSerializationExtensions.JsonDocumentOptions);
            return DeserializeLogAggregationResponse(document.RootElement, ModelSerializationExtensions.WireOptions);
        }

        void IJsonModel<LogAggregationResponse>.Write(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            writer.WriteStartObject();
            JsonModelWriteCore(writer, options);
            writer.WriteEndObject();
        }

        protected virtual void JsonModelWriteCore(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<LogAggregationResponse>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(LogAggregationResponse)} does not support writing '{format}' format.");
            }
            writer.WritePropertyName("results"u8);
            writer.WriteStartArray();
            foreach (LogAggregationBucket item in Results)
            {
                writer.WriteObjectValue(item, options);
            }
            writer.WriteEndArray();
            writer.WritePropertyName("total_count"u8);
            writer.WriteNumberValue(TotalCount);
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

        LogAggregationResponse IJsonModel<LogAggregationResponse>.Create(ref Utf8JsonReader reader, ModelReaderWriterOptions options) => JsonModelCreateCore(ref reader, options);

        protected virtual LogAggregationResponse JsonModelCreateCore(ref Utf8JsonReader reader, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<LogAggregationResponse>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(LogAggregationResponse)} does not support reading '{format}' format.");
            }
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            return DeserializeLogAggregationResponse(document.RootElement, options);
        }

        internal static LogAggregationResponse DeserializeLogAggregationResponse(JsonElement element, ModelReaderWriterOptions options)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }
            IList<LogAggregationBucket> results = default;
            long totalCount = default;
            IDictionary<string, BinaryData> additionalBinaryDataProperties = new ChangeTrackingDictionary<string, BinaryData>();
            foreach (var prop in element.EnumerateObject())
            {
                if (prop.NameEquals("results"u8))
                {
                    List<LogAggregationBucket> array = new List<LogAggregationBucket>();
                    foreach (var item in prop.Value.EnumerateArray())
                    {
                        array.Add(LogAggregationBucket.DeserializeLogAggregationBucket(item, options));
                    }
                    results = array;
                    continue;
                }
                if (prop.NameEquals("total_count"u8))
                {
                    totalCount = prop.Value.GetInt64();
                    continue;
                }
                if (options.Format != "W")
                {
                    additionalBinaryDataProperties.Add(prop.Name, BinaryData.FromString(prop.Value.GetRawText()));
                }
            }
            return new LogAggregationResponse(results, totalCount, additionalBinaryDataProperties);
        }
    }
}
