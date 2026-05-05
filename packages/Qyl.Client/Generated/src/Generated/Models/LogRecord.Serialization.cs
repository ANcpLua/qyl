
#nullable disable

using System;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Text.Json;
using Qyl.Client;
using Qyl.Common;
using Qyl.OTel.Enums;
using Qyl.OTel.Resource;

namespace Qyl.OTel.Logs
{
    public partial class LogRecord : IJsonModel<LogRecord>
    {
        internal LogRecord()
        {
        }

        protected virtual LogRecord PersistableModelCreateCore(BinaryData data, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<LogRecord>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    using (JsonDocument document = JsonDocument.Parse(data, ModelSerializationExtensions.JsonDocumentOptions))
                    {
                        return DeserializeLogRecord(document.RootElement, options);
                    }
                default:
                    throw new FormatException($"The model {nameof(LogRecord)} does not support reading '{options.Format}' format.");
            }
        }

        protected virtual BinaryData PersistableModelWriteCore(ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<LogRecord>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    return ModelReaderWriter.Write(this, options, QylClientContext.Default);
                default:
                    throw new FormatException($"The model {nameof(LogRecord)} does not support writing '{options.Format}' format.");
            }
        }

        BinaryData IPersistableModel<LogRecord>.Write(ModelReaderWriterOptions options) => PersistableModelWriteCore(options);

        LogRecord IPersistableModel<LogRecord>.Create(BinaryData data, ModelReaderWriterOptions options) => PersistableModelCreateCore(data, options);

        string IPersistableModel<LogRecord>.GetFormatFromOptions(ModelReaderWriterOptions options) => "J";

        void IJsonModel<LogRecord>.Write(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            writer.WriteStartObject();
            JsonModelWriteCore(writer, options);
            writer.WriteEndObject();
        }

        protected virtual void JsonModelWriteCore(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<LogRecord>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(LogRecord)} does not support writing '{format}' format.");
            }
            writer.WritePropertyName("time_unix_nano"u8);
            writer.WriteNumberValue(TimeUnixNano);
            writer.WritePropertyName("observed_time_unix_nano"u8);
            writer.WriteNumberValue(ObservedTimeUnixNano);
            writer.WritePropertyName("severity_number"u8);
            writer.WriteNumberValue((int)SeverityNumber);
            if (Optional.IsDefined(SeverityText))
            {
                writer.WritePropertyName("severity_text"u8);
                writer.WriteStringValue(SeverityText.Value.ToSerialString());
            }
            writer.WritePropertyName("body"u8);
#if NET6_0_OR_GREATER
            writer.WriteRawValue(Body);
#else
            using (JsonDocument document = JsonDocument.Parse(Body))
            {
                JsonSerializer.Serialize(writer, document.RootElement);
            }
#endif
            if (Optional.IsCollectionDefined(Attributes))
            {
                writer.WritePropertyName("attributes"u8);
                writer.WriteStartArray();
                foreach (Common.Attribute item in Attributes)
                {
                    writer.WriteObjectValue(item, options);
                }
                writer.WriteEndArray();
            }
            if (Optional.IsDefined(DroppedAttributesCount))
            {
                writer.WritePropertyName("dropped_attributes_count"u8);
                writer.WriteNumberValue(DroppedAttributesCount.Value);
            }
            if (Optional.IsDefined(Flags))
            {
                writer.WritePropertyName("flags"u8);
                writer.WriteNumberValue(Flags.Value);
            }
            if (Optional.IsDefined(TraceId))
            {
                writer.WritePropertyName("trace_id"u8);
                writer.WriteStringValue(TraceId);
            }
            if (Optional.IsDefined(SpanId))
            {
                writer.WritePropertyName("span_id"u8);
                writer.WriteStringValue(SpanId);
            }
            writer.WritePropertyName("resource"u8);
            writer.WriteObjectValue(Resource, options);
            if (Optional.IsDefined(InstrumentationScope))
            {
                writer.WritePropertyName("instrumentation_scope"u8);
                writer.WriteObjectValue(InstrumentationScope, options);
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

        LogRecord IJsonModel<LogRecord>.Create(ref Utf8JsonReader reader, ModelReaderWriterOptions options) => JsonModelCreateCore(ref reader, options);

        protected virtual LogRecord JsonModelCreateCore(ref Utf8JsonReader reader, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<LogRecord>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(LogRecord)} does not support reading '{format}' format.");
            }
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            return DeserializeLogRecord(document.RootElement, options);
        }

        internal static LogRecord DeserializeLogRecord(JsonElement element, ModelReaderWriterOptions options)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }
            long timeUnixNano = default;
            long observedTimeUnixNano = default;
            SeverityNumber severityNumber = default;
            SeverityText? severityText = default;
            BinaryData body = default;
            IList<Common.Attribute> attributes = default;
            long? droppedAttributesCount = default;
            int? flags = default;
            string traceId = default;
            string spanId = default;
            Resource.Resource resource = default;
            InstrumentationScope instrumentationScope = default;
            IDictionary<string, BinaryData> additionalBinaryDataProperties = new ChangeTrackingDictionary<string, BinaryData>();
            foreach (var prop in element.EnumerateObject())
            {
                if (prop.NameEquals("time_unix_nano"u8))
                {
                    timeUnixNano = prop.Value.GetInt64();
                    continue;
                }
                if (prop.NameEquals("observed_time_unix_nano"u8))
                {
                    observedTimeUnixNano = prop.Value.GetInt64();
                    continue;
                }
                if (prop.NameEquals("severity_number"u8))
                {
                    severityNumber = prop.Value.GetInt32().ToSeverityNumber();
                    continue;
                }
                if (prop.NameEquals("severity_text"u8))
                {
                    if (prop.Value.ValueKind == JsonValueKind.Null)
                    {
                        continue;
                    }
                    severityText = prop.Value.GetString().ToSeverityText();
                    continue;
                }
                if (prop.NameEquals("body"u8))
                {
                    body = BinaryData.FromString(prop.Value.GetRawText());
                    continue;
                }
                if (prop.NameEquals("attributes"u8))
                {
                    if (prop.Value.ValueKind == JsonValueKind.Null)
                    {
                        continue;
                    }
                    List<Common.Attribute> array = new List<Common.Attribute>();
                    foreach (var item in prop.Value.EnumerateArray())
                    {
                        array.Add(Common.Attribute.DeserializeAttribute(item, options));
                    }
                    attributes = array;
                    continue;
                }
                if (prop.NameEquals("dropped_attributes_count"u8))
                {
                    if (prop.Value.ValueKind == JsonValueKind.Null)
                    {
                        continue;
                    }
                    droppedAttributesCount = prop.Value.GetInt64();
                    continue;
                }
                if (prop.NameEquals("flags"u8))
                {
                    if (prop.Value.ValueKind == JsonValueKind.Null)
                    {
                        continue;
                    }
                    flags = prop.Value.GetInt32();
                    continue;
                }
                if (prop.NameEquals("trace_id"u8))
                {
                    traceId = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("span_id"u8))
                {
                    spanId = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("resource"u8))
                {
                    resource = OTel.Resource.Resource.DeserializeResource(prop.Value, options);
                    continue;
                }
                if (prop.NameEquals("instrumentation_scope"u8))
                {
                    if (prop.Value.ValueKind == JsonValueKind.Null)
                    {
                        continue;
                    }
                    instrumentationScope = InstrumentationScope.DeserializeInstrumentationScope(prop.Value, options);
                    continue;
                }
                if (options.Format != "W")
                {
                    additionalBinaryDataProperties.Add(prop.Name, BinaryData.FromString(prop.Value.GetRawText()));
                }
            }
            return new LogRecord(
                timeUnixNano,
                observedTimeUnixNano,
                severityNumber,
                severityText,
                body,
                attributes ?? new ChangeTrackingList<Common.Attribute>(),
                droppedAttributesCount,
                flags,
                traceId,
                spanId,
                resource,
                instrumentationScope,
                additionalBinaryDataProperties);
        }
    }
}
