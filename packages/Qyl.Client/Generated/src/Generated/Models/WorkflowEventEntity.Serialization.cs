
#nullable disable

using System;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Text.Json;
using Qyl.Client;

namespace Qyl.Domains.Workflow
{
    public partial class WorkflowEventEntity : IJsonModel<WorkflowEventEntity>
    {
        internal WorkflowEventEntity()
        {
        }

        protected virtual WorkflowEventEntity PersistableModelCreateCore(BinaryData data, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<WorkflowEventEntity>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    using (JsonDocument document = JsonDocument.Parse(data, ModelSerializationExtensions.JsonDocumentOptions))
                    {
                        return DeserializeWorkflowEventEntity(document.RootElement, options);
                    }
                default:
                    throw new FormatException($"The model {nameof(WorkflowEventEntity)} does not support reading '{options.Format}' format.");
            }
        }

        protected virtual BinaryData PersistableModelWriteCore(ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<WorkflowEventEntity>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    return ModelReaderWriter.Write(this, options, QylClientContext.Default);
                default:
                    throw new FormatException($"The model {nameof(WorkflowEventEntity)} does not support writing '{options.Format}' format.");
            }
        }

        BinaryData IPersistableModel<WorkflowEventEntity>.Write(ModelReaderWriterOptions options) => PersistableModelWriteCore(options);

        WorkflowEventEntity IPersistableModel<WorkflowEventEntity>.Create(BinaryData data, ModelReaderWriterOptions options) => PersistableModelCreateCore(data, options);

        string IPersistableModel<WorkflowEventEntity>.GetFormatFromOptions(ModelReaderWriterOptions options) => "J";

        void IJsonModel<WorkflowEventEntity>.Write(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            writer.WriteStartObject();
            JsonModelWriteCore(writer, options);
            writer.WriteEndObject();
        }

        protected virtual void JsonModelWriteCore(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<WorkflowEventEntity>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(WorkflowEventEntity)} does not support writing '{format}' format.");
            }
            writer.WritePropertyName("id"u8);
            writer.WriteStringValue(Id);
            writer.WritePropertyName("run_id"u8);
            writer.WriteStringValue(RunId);
            if (Optional.IsDefined(NodeId))
            {
                writer.WritePropertyName("node_id"u8);
                writer.WriteStringValue(NodeId);
            }
            writer.WritePropertyName("event_type"u8);
            writer.WriteStringValue(EventType);
            writer.WritePropertyName("event_name"u8);
            writer.WriteStringValue(EventName);
            if (Optional.IsDefined(PayloadJson))
            {
                writer.WritePropertyName("payload_json"u8);
                writer.WriteStringValue(PayloadJson);
            }
            writer.WritePropertyName("sequence_number"u8);
            writer.WriteNumberValue(SequenceNumber);
            if (Optional.IsDefined(Source))
            {
                writer.WritePropertyName("source"u8);
                writer.WriteStringValue(Source);
            }
            if (Optional.IsDefined(CorrelationId))
            {
                writer.WritePropertyName("correlation_id"u8);
                writer.WriteStringValue(CorrelationId);
            }
            writer.WritePropertyName("timestamp"u8);
            writer.WriteStringValue(Timestamp, "O");
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

        WorkflowEventEntity IJsonModel<WorkflowEventEntity>.Create(ref Utf8JsonReader reader, ModelReaderWriterOptions options) => JsonModelCreateCore(ref reader, options);

        protected virtual WorkflowEventEntity JsonModelCreateCore(ref Utf8JsonReader reader, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<WorkflowEventEntity>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(WorkflowEventEntity)} does not support reading '{format}' format.");
            }
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            return DeserializeWorkflowEventEntity(document.RootElement, options);
        }

        internal static WorkflowEventEntity DeserializeWorkflowEventEntity(JsonElement element, ModelReaderWriterOptions options)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }
            string id = default;
            string runId = default;
            string nodeId = default;
            string eventType = default;
            string eventName = default;
            string payloadJson = default;
            long sequenceNumber = default;
            string source = default;
            string correlationId = default;
            DateTimeOffset timestamp = default;
            IDictionary<string, BinaryData> additionalBinaryDataProperties = new ChangeTrackingDictionary<string, BinaryData>();
            foreach (var prop in element.EnumerateObject())
            {
                if (prop.NameEquals("id"u8))
                {
                    id = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("run_id"u8))
                {
                    runId = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("node_id"u8))
                {
                    nodeId = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("event_type"u8))
                {
                    eventType = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("event_name"u8))
                {
                    eventName = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("payload_json"u8))
                {
                    payloadJson = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("sequence_number"u8))
                {
                    sequenceNumber = prop.Value.GetInt64();
                    continue;
                }
                if (prop.NameEquals("source"u8))
                {
                    source = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("correlation_id"u8))
                {
                    correlationId = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("timestamp"u8))
                {
                    timestamp = prop.Value.GetDateTimeOffset("O");
                    continue;
                }
                if (options.Format != "W")
                {
                    additionalBinaryDataProperties.Add(prop.Name, BinaryData.FromString(prop.Value.GetRawText()));
                }
            }
            return new WorkflowEventEntity(
                id,
                runId,
                nodeId,
                eventType,
                eventName,
                payloadJson,
                sequenceNumber,
                source,
                correlationId,
                timestamp,
                additionalBinaryDataProperties);
        }
    }
}
