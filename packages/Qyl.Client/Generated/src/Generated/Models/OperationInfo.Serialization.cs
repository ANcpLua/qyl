
#nullable disable

using System;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Text.Json;
using Qyl.Client;
using Qyl.OTel.Enums;

namespace Qyl.Api
{
    public partial class OperationInfo : IJsonModel<OperationInfo>
    {
        internal OperationInfo()
        {
        }

        protected virtual OperationInfo PersistableModelCreateCore(BinaryData data, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<OperationInfo>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    using (JsonDocument document = JsonDocument.Parse(data, ModelSerializationExtensions.JsonDocumentOptions))
                    {
                        return DeserializeOperationInfo(document.RootElement, options);
                    }
                default:
                    throw new FormatException($"The model {nameof(OperationInfo)} does not support reading '{options.Format}' format.");
            }
        }

        protected virtual BinaryData PersistableModelWriteCore(ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<OperationInfo>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    return ModelReaderWriter.Write(this, options, QylClientContext.Default);
                default:
                    throw new FormatException($"The model {nameof(OperationInfo)} does not support writing '{options.Format}' format.");
            }
        }

        BinaryData IPersistableModel<OperationInfo>.Write(ModelReaderWriterOptions options) => PersistableModelWriteCore(options);

        OperationInfo IPersistableModel<OperationInfo>.Create(BinaryData data, ModelReaderWriterOptions options) => PersistableModelCreateCore(data, options);

        string IPersistableModel<OperationInfo>.GetFormatFromOptions(ModelReaderWriterOptions options) => "J";

        void IJsonModel<OperationInfo>.Write(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            writer.WriteStartObject();
            JsonModelWriteCore(writer, options);
            writer.WriteEndObject();
        }

        protected virtual void JsonModelWriteCore(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<OperationInfo>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(OperationInfo)} does not support writing '{format}' format.");
            }
            writer.WritePropertyName("name"u8);
            writer.WriteStringValue(Name);
            writer.WritePropertyName("span_kind"u8);
            writer.WriteNumberValue((int)SpanKind);
            writer.WritePropertyName("request_count"u8);
            writer.WriteNumberValue(RequestCount);
            writer.WritePropertyName("error_count"u8);
            writer.WriteNumberValue(ErrorCount);
            writer.WritePropertyName("avg_duration_ms"u8);
            writer.WriteNumberValue(AvgDurationMs);
            writer.WritePropertyName("p99_duration_ms"u8);
            writer.WriteNumberValue(P99DurationMs);
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

        OperationInfo IJsonModel<OperationInfo>.Create(ref Utf8JsonReader reader, ModelReaderWriterOptions options) => JsonModelCreateCore(ref reader, options);

        protected virtual OperationInfo JsonModelCreateCore(ref Utf8JsonReader reader, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<OperationInfo>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(OperationInfo)} does not support reading '{format}' format.");
            }
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            return DeserializeOperationInfo(document.RootElement, options);
        }

        internal static OperationInfo DeserializeOperationInfo(JsonElement element, ModelReaderWriterOptions options)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }
            string name = default;
            SpanKind spanKind = default;
            long requestCount = default;
            long errorCount = default;
            double avgDurationMs = default;
            double p99DurationMs = default;
            IDictionary<string, BinaryData> additionalBinaryDataProperties = new ChangeTrackingDictionary<string, BinaryData>();
            foreach (var prop in element.EnumerateObject())
            {
                if (prop.NameEquals("name"u8))
                {
                    name = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("span_kind"u8))
                {
                    spanKind = prop.Value.GetInt32().ToSpanKind();
                    continue;
                }
                if (prop.NameEquals("request_count"u8))
                {
                    requestCount = prop.Value.GetInt64();
                    continue;
                }
                if (prop.NameEquals("error_count"u8))
                {
                    errorCount = prop.Value.GetInt64();
                    continue;
                }
                if (prop.NameEquals("avg_duration_ms"u8))
                {
                    avgDurationMs = prop.Value.GetDouble();
                    continue;
                }
                if (prop.NameEquals("p99_duration_ms"u8))
                {
                    p99DurationMs = prop.Value.GetDouble();
                    continue;
                }
                if (options.Format != "W")
                {
                    additionalBinaryDataProperties.Add(prop.Name, BinaryData.FromString(prop.Value.GetRawText()));
                }
            }
            return new OperationInfo(
                name,
                spanKind,
                requestCount,
                errorCount,
                avgDurationMs,
                p99DurationMs,
                additionalBinaryDataProperties);
        }
    }
}
