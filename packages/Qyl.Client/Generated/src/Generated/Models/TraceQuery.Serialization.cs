
#nullable disable

using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Text.Json;
using Qyl.Client;
using Qyl.OTel.Enums;

namespace Qyl.Api
{
    public partial class TraceQuery : IJsonModel<TraceQuery>
    {
        protected virtual TraceQuery PersistableModelCreateCore(BinaryData data, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<TraceQuery>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    using (JsonDocument document = JsonDocument.Parse(data, ModelSerializationExtensions.JsonDocumentOptions))
                    {
                        return DeserializeTraceQuery(document.RootElement, options);
                    }
                default:
                    throw new FormatException($"The model {nameof(TraceQuery)} does not support reading '{options.Format}' format.");
            }
        }

        protected virtual BinaryData PersistableModelWriteCore(ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<TraceQuery>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    return ModelReaderWriter.Write(this, options, QylClientContext.Default);
                default:
                    throw new FormatException($"The model {nameof(TraceQuery)} does not support writing '{options.Format}' format.");
            }
        }

        BinaryData IPersistableModel<TraceQuery>.Write(ModelReaderWriterOptions options) => PersistableModelWriteCore(options);

        TraceQuery IPersistableModel<TraceQuery>.Create(BinaryData data, ModelReaderWriterOptions options) => PersistableModelCreateCore(data, options);

        string IPersistableModel<TraceQuery>.GetFormatFromOptions(ModelReaderWriterOptions options) => "J";

        public static implicit operator BinaryContent(TraceQuery traceQuery)
        {
            if (traceQuery == null)
            {
                return null;
            }
            return BinaryContent.Create(traceQuery, ModelSerializationExtensions.WireOptions);
        }

        void IJsonModel<TraceQuery>.Write(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            writer.WriteStartObject();
            JsonModelWriteCore(writer, options);
            writer.WriteEndObject();
        }

        protected virtual void JsonModelWriteCore(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<TraceQuery>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(TraceQuery)} does not support writing '{format}' format.");
            }
            if (Optional.IsDefined(Query))
            {
                writer.WritePropertyName("query"u8);
                writer.WriteStringValue(Query);
            }
            if (Optional.IsDefined(ServiceName))
            {
                writer.WritePropertyName("service_name"u8);
                writer.WriteStringValue(ServiceName);
            }
            if (Optional.IsDefined(OperationName))
            {
                writer.WritePropertyName("operation_name"u8);
                writer.WriteStringValue(OperationName);
            }
            if (Optional.IsDefined(MinDurationMs))
            {
                writer.WritePropertyName("min_duration_ms"u8);
                writer.WriteNumberValue(MinDurationMs.Value);
            }
            if (Optional.IsDefined(MaxDurationMs))
            {
                writer.WritePropertyName("max_duration_ms"u8);
                writer.WriteNumberValue(MaxDurationMs.Value);
            }
            if (Optional.IsDefined(Status))
            {
                writer.WritePropertyName("status"u8);
                writer.WriteNumberValue((int)Status.Value);
            }
            if (Optional.IsDefined(StartTime))
            {
                writer.WritePropertyName("start_time"u8);
                writer.WriteStringValue(StartTime.Value, "O");
            }
            if (Optional.IsDefined(EndTime))
            {
                writer.WritePropertyName("end_time"u8);
                writer.WriteStringValue(EndTime.Value, "O");
            }
            if (Optional.IsCollectionDefined(Tags))
            {
                writer.WritePropertyName("tags"u8);
                writer.WriteStartObject();
                foreach (var item in Tags)
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
            if (Optional.IsDefined(Limit))
            {
                writer.WritePropertyName("limit"u8);
                writer.WriteNumberValue(Limit.Value);
            }
            if (Optional.IsDefined(Cursor))
            {
                writer.WritePropertyName("cursor"u8);
                writer.WriteStringValue(Cursor);
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

        TraceQuery IJsonModel<TraceQuery>.Create(ref Utf8JsonReader reader, ModelReaderWriterOptions options) => JsonModelCreateCore(ref reader, options);

        protected virtual TraceQuery JsonModelCreateCore(ref Utf8JsonReader reader, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<TraceQuery>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(TraceQuery)} does not support reading '{format}' format.");
            }
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            return DeserializeTraceQuery(document.RootElement, options);
        }

        internal static TraceQuery DeserializeTraceQuery(JsonElement element, ModelReaderWriterOptions options)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }
            string query = default;
            string serviceName = default;
            string operationName = default;
            long? minDurationMs = default;
            long? maxDurationMs = default;
            SpanStatusCode? status = default;
            DateTimeOffset? startTime = default;
            DateTimeOffset? endTime = default;
            IDictionary<string, string> tags = default;
            int? limit = default;
            string cursor = default;
            IDictionary<string, BinaryData> additionalBinaryDataProperties = new ChangeTrackingDictionary<string, BinaryData>();
            foreach (var prop in element.EnumerateObject())
            {
                if (prop.NameEquals("query"u8))
                {
                    query = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("service_name"u8))
                {
                    serviceName = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("operation_name"u8))
                {
                    operationName = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("min_duration_ms"u8))
                {
                    if (prop.Value.ValueKind == JsonValueKind.Null)
                    {
                        continue;
                    }
                    minDurationMs = prop.Value.GetInt64();
                    continue;
                }
                if (prop.NameEquals("max_duration_ms"u8))
                {
                    if (prop.Value.ValueKind == JsonValueKind.Null)
                    {
                        continue;
                    }
                    maxDurationMs = prop.Value.GetInt64();
                    continue;
                }
                if (prop.NameEquals("status"u8))
                {
                    if (prop.Value.ValueKind == JsonValueKind.Null)
                    {
                        continue;
                    }
                    status = prop.Value.GetInt32().ToSpanStatusCode();
                    continue;
                }
                if (prop.NameEquals("start_time"u8))
                {
                    if (prop.Value.ValueKind == JsonValueKind.Null)
                    {
                        continue;
                    }
                    startTime = prop.Value.GetDateTimeOffset("O");
                    continue;
                }
                if (prop.NameEquals("end_time"u8))
                {
                    if (prop.Value.ValueKind == JsonValueKind.Null)
                    {
                        continue;
                    }
                    endTime = prop.Value.GetDateTimeOffset("O");
                    continue;
                }
                if (prop.NameEquals("tags"u8))
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
                    tags = dictionary;
                    continue;
                }
                if (prop.NameEquals("limit"u8))
                {
                    if (prop.Value.ValueKind == JsonValueKind.Null)
                    {
                        continue;
                    }
                    limit = prop.Value.GetInt32();
                    continue;
                }
                if (prop.NameEquals("cursor"u8))
                {
                    cursor = prop.Value.GetString();
                    continue;
                }
                if (options.Format != "W")
                {
                    additionalBinaryDataProperties.Add(prop.Name, BinaryData.FromString(prop.Value.GetRawText()));
                }
            }
            return new TraceQuery(
                query,
                serviceName,
                operationName,
                minDurationMs,
                maxDurationMs,
                status,
                startTime,
                endTime,
                tags ?? new ChangeTrackingDictionary<string, string>(),
                limit,
                cursor,
                additionalBinaryDataProperties);
        }
    }
}
