
#nullable disable

using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Text.Json;
using Qyl.Client;

namespace Qyl.Domains.Observe.Error
{
    public partial class ErrorStats : IJsonModel<ErrorStats>
    {
        internal ErrorStats()
        {
        }

        protected virtual ErrorStats PersistableModelCreateCore(BinaryData data, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<ErrorStats>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    using (JsonDocument document = JsonDocument.Parse(data, ModelSerializationExtensions.JsonDocumentOptions))
                    {
                        return DeserializeErrorStats(document.RootElement, options);
                    }
                default:
                    throw new FormatException($"The model {nameof(ErrorStats)} does not support reading '{options.Format}' format.");
            }
        }

        protected virtual BinaryData PersistableModelWriteCore(ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<ErrorStats>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    return ModelReaderWriter.Write(this, options, QylClientContext.Default);
                default:
                    throw new FormatException($"The model {nameof(ErrorStats)} does not support writing '{options.Format}' format.");
            }
        }

        BinaryData IPersistableModel<ErrorStats>.Write(ModelReaderWriterOptions options) => PersistableModelWriteCore(options);

        ErrorStats IPersistableModel<ErrorStats>.Create(BinaryData data, ModelReaderWriterOptions options) => PersistableModelCreateCore(data, options);

        string IPersistableModel<ErrorStats>.GetFormatFromOptions(ModelReaderWriterOptions options) => "J";

        public static explicit operator ErrorStats(ClientResult result)
        {
            PipelineResponse response = result.GetRawResponse();
            using JsonDocument document = JsonDocument.Parse(response.Content, ModelSerializationExtensions.JsonDocumentOptions);
            return DeserializeErrorStats(document.RootElement, ModelSerializationExtensions.WireOptions);
        }

        void IJsonModel<ErrorStats>.Write(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            writer.WriteStartObject();
            JsonModelWriteCore(writer, options);
            writer.WriteEndObject();
        }

        protected virtual void JsonModelWriteCore(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<ErrorStats>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(ErrorStats)} does not support writing '{format}' format.");
            }
            writer.WritePropertyName("total_count"u8);
            writer.WriteNumberValue(TotalCount);
            writer.WritePropertyName("unique_types"u8);
            writer.WriteNumberValue(UniqueTypes);
            writer.WritePropertyName("error_rate"u8);
            writer.WriteNumberValue(ErrorRate);
            writer.WritePropertyName("by_category"u8);
            writer.WriteStartArray();
            foreach (ErrorCategoryStats item in ByCategory)
            {
                writer.WriteObjectValue(item, options);
            }
            writer.WriteEndArray();
            if (Optional.IsCollectionDefined(ByService))
            {
                writer.WritePropertyName("by_service"u8);
                writer.WriteStartArray();
                foreach (ErrorServiceStats item in ByService)
                {
                    writer.WriteObjectValue(item, options);
                }
                writer.WriteEndArray();
            }
            writer.WritePropertyName("top_errors"u8);
            writer.WriteStartArray();
            foreach (ErrorTypeStats item in TopErrors)
            {
                writer.WriteObjectValue(item, options);
            }
            writer.WriteEndArray();
            writer.WritePropertyName("trend"u8);
            writer.WriteStringValue(Trend.ToSerialString());
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

        ErrorStats IJsonModel<ErrorStats>.Create(ref Utf8JsonReader reader, ModelReaderWriterOptions options) => JsonModelCreateCore(ref reader, options);

        protected virtual ErrorStats JsonModelCreateCore(ref Utf8JsonReader reader, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<ErrorStats>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(ErrorStats)} does not support reading '{format}' format.");
            }
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            return DeserializeErrorStats(document.RootElement, options);
        }

        internal static ErrorStats DeserializeErrorStats(JsonElement element, ModelReaderWriterOptions options)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }
            long totalCount = default;
            int uniqueTypes = default;
            double errorRate = default;
            IList<ErrorCategoryStats> byCategory = default;
            IList<ErrorServiceStats> byService = default;
            IList<ErrorTypeStats> topErrors = default;
            ErrorTrend trend = default;
            IDictionary<string, BinaryData> additionalBinaryDataProperties = new ChangeTrackingDictionary<string, BinaryData>();
            foreach (var prop in element.EnumerateObject())
            {
                if (prop.NameEquals("total_count"u8))
                {
                    totalCount = prop.Value.GetInt64();
                    continue;
                }
                if (prop.NameEquals("unique_types"u8))
                {
                    uniqueTypes = prop.Value.GetInt32();
                    continue;
                }
                if (prop.NameEquals("error_rate"u8))
                {
                    errorRate = prop.Value.GetDouble();
                    continue;
                }
                if (prop.NameEquals("by_category"u8))
                {
                    List<ErrorCategoryStats> array = new List<ErrorCategoryStats>();
                    foreach (var item in prop.Value.EnumerateArray())
                    {
                        array.Add(ErrorCategoryStats.DeserializeErrorCategoryStats(item, options));
                    }
                    byCategory = array;
                    continue;
                }
                if (prop.NameEquals("by_service"u8))
                {
                    if (prop.Value.ValueKind == JsonValueKind.Null)
                    {
                        continue;
                    }
                    List<ErrorServiceStats> array = new List<ErrorServiceStats>();
                    foreach (var item in prop.Value.EnumerateArray())
                    {
                        array.Add(ErrorServiceStats.DeserializeErrorServiceStats(item, options));
                    }
                    byService = array;
                    continue;
                }
                if (prop.NameEquals("top_errors"u8))
                {
                    List<ErrorTypeStats> array = new List<ErrorTypeStats>();
                    foreach (var item in prop.Value.EnumerateArray())
                    {
                        array.Add(ErrorTypeStats.DeserializeErrorTypeStats(item, options));
                    }
                    topErrors = array;
                    continue;
                }
                if (prop.NameEquals("trend"u8))
                {
                    trend = prop.Value.GetString().ToErrorTrend();
                    continue;
                }
                if (options.Format != "W")
                {
                    additionalBinaryDataProperties.Add(prop.Name, BinaryData.FromString(prop.Value.GetRawText()));
                }
            }
            return new ErrorStats(
                totalCount,
                uniqueTypes,
                errorRate,
                byCategory,
                byService ?? new ChangeTrackingList<ErrorServiceStats>(),
                topErrors,
                trend,
                additionalBinaryDataProperties);
        }
    }
}
