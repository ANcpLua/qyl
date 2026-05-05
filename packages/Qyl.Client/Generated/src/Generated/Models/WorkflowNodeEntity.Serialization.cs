
#nullable disable

using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Text.Json;
using Qyl.Client;

namespace Qyl.Domains.Workflow
{
    public partial class WorkflowNodeEntity : IJsonModel<WorkflowNodeEntity>
    {
        internal WorkflowNodeEntity()
        {
        }

        protected virtual WorkflowNodeEntity PersistableModelCreateCore(BinaryData data, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<WorkflowNodeEntity>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    using (JsonDocument document = JsonDocument.Parse(data, ModelSerializationExtensions.JsonDocumentOptions))
                    {
                        return DeserializeWorkflowNodeEntity(document.RootElement, options);
                    }
                default:
                    throw new FormatException($"The model {nameof(WorkflowNodeEntity)} does not support reading '{options.Format}' format.");
            }
        }

        protected virtual BinaryData PersistableModelWriteCore(ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<WorkflowNodeEntity>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    return ModelReaderWriter.Write(this, options, QylClientContext.Default);
                default:
                    throw new FormatException($"The model {nameof(WorkflowNodeEntity)} does not support writing '{options.Format}' format.");
            }
        }

        BinaryData IPersistableModel<WorkflowNodeEntity>.Write(ModelReaderWriterOptions options) => PersistableModelWriteCore(options);

        WorkflowNodeEntity IPersistableModel<WorkflowNodeEntity>.Create(BinaryData data, ModelReaderWriterOptions options) => PersistableModelCreateCore(data, options);

        string IPersistableModel<WorkflowNodeEntity>.GetFormatFromOptions(ModelReaderWriterOptions options) => "J";

        public static explicit operator WorkflowNodeEntity(ClientResult result)
        {
            PipelineResponse response = result.GetRawResponse();
            using JsonDocument document = JsonDocument.Parse(response.Content, ModelSerializationExtensions.JsonDocumentOptions);
            return DeserializeWorkflowNodeEntity(document.RootElement, ModelSerializationExtensions.WireOptions);
        }

        void IJsonModel<WorkflowNodeEntity>.Write(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            writer.WriteStartObject();
            JsonModelWriteCore(writer, options);
            writer.WriteEndObject();
        }

        protected virtual void JsonModelWriteCore(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<WorkflowNodeEntity>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(WorkflowNodeEntity)} does not support writing '{format}' format.");
            }
            writer.WritePropertyName("id"u8);
            writer.WriteStringValue(Id);
            writer.WritePropertyName("run_id"u8);
            writer.WriteStringValue(RunId);
            writer.WritePropertyName("node_id"u8);
            writer.WriteStringValue(NodeId);
            writer.WritePropertyName("node_type"u8);
            writer.WriteStringValue(NodeType.ToSerialString());
            writer.WritePropertyName("node_name"u8);
            writer.WriteStringValue(NodeName);
            writer.WritePropertyName("attempt"u8);
            writer.WriteNumberValue(Attempt);
            if (Optional.IsDefined(InputJson))
            {
                writer.WritePropertyName("input_json"u8);
                writer.WriteStringValue(InputJson);
            }
            if (Optional.IsDefined(OutputJson))
            {
                writer.WritePropertyName("output_json"u8);
                writer.WriteStringValue(OutputJson);
            }
            writer.WritePropertyName("status"u8);
            writer.WriteStringValue(Status.ToSerialString());
            if (Optional.IsDefined(ErrorMessage))
            {
                writer.WritePropertyName("error_message"u8);
                writer.WriteStringValue(ErrorMessage);
            }
            writer.WritePropertyName("retry_count"u8);
            writer.WriteNumberValue(RetryCount);
            writer.WritePropertyName("max_retries"u8);
            writer.WriteNumberValue(MaxRetries);
            if (Optional.IsDefined(TimeoutMs))
            {
                writer.WritePropertyName("timeout_ms"u8);
                writer.WriteNumberValue(TimeoutMs.Value);
            }
            if (Optional.IsDefined(StartedAt))
            {
                writer.WritePropertyName("started_at"u8);
                writer.WriteStringValue(StartedAt.Value, "O");
            }
            if (Optional.IsDefined(CompletedAt))
            {
                writer.WritePropertyName("completed_at"u8);
                writer.WriteStringValue(CompletedAt.Value, "O");
            }
            if (Optional.IsDefined(DurationMs))
            {
                writer.WritePropertyName("duration_ms"u8);
                writer.WriteNumberValue(DurationMs.Value);
            }
            writer.WritePropertyName("created_at"u8);
            writer.WriteStringValue(CreatedAt, "O");
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

        WorkflowNodeEntity IJsonModel<WorkflowNodeEntity>.Create(ref Utf8JsonReader reader, ModelReaderWriterOptions options) => JsonModelCreateCore(ref reader, options);

        protected virtual WorkflowNodeEntity JsonModelCreateCore(ref Utf8JsonReader reader, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<WorkflowNodeEntity>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(WorkflowNodeEntity)} does not support reading '{format}' format.");
            }
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            return DeserializeWorkflowNodeEntity(document.RootElement, options);
        }

        internal static WorkflowNodeEntity DeserializeWorkflowNodeEntity(JsonElement element, ModelReaderWriterOptions options)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }
            string id = default;
            string runId = default;
            string nodeId = default;
            WorkflowNodeType nodeType = default;
            string nodeName = default;
            int attempt = default;
            string inputJson = default;
            string outputJson = default;
            WorkflowRunStatus status = default;
            string errorMessage = default;
            int retryCount = default;
            int maxRetries = default;
            int? timeoutMs = default;
            DateTimeOffset? startedAt = default;
            DateTimeOffset? completedAt = default;
            int? durationMs = default;
            DateTimeOffset createdAt = default;
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
                if (prop.NameEquals("node_type"u8))
                {
                    nodeType = prop.Value.GetString().ToWorkflowNodeType();
                    continue;
                }
                if (prop.NameEquals("node_name"u8))
                {
                    nodeName = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("attempt"u8))
                {
                    attempt = prop.Value.GetInt32();
                    continue;
                }
                if (prop.NameEquals("input_json"u8))
                {
                    inputJson = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("output_json"u8))
                {
                    outputJson = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("status"u8))
                {
                    status = prop.Value.GetString().ToWorkflowRunStatus();
                    continue;
                }
                if (prop.NameEquals("error_message"u8))
                {
                    errorMessage = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("retry_count"u8))
                {
                    retryCount = prop.Value.GetInt32();
                    continue;
                }
                if (prop.NameEquals("max_retries"u8))
                {
                    maxRetries = prop.Value.GetInt32();
                    continue;
                }
                if (prop.NameEquals("timeout_ms"u8))
                {
                    if (prop.Value.ValueKind == JsonValueKind.Null)
                    {
                        continue;
                    }
                    timeoutMs = prop.Value.GetInt32();
                    continue;
                }
                if (prop.NameEquals("started_at"u8))
                {
                    if (prop.Value.ValueKind == JsonValueKind.Null)
                    {
                        continue;
                    }
                    startedAt = prop.Value.GetDateTimeOffset("O");
                    continue;
                }
                if (prop.NameEquals("completed_at"u8))
                {
                    if (prop.Value.ValueKind == JsonValueKind.Null)
                    {
                        continue;
                    }
                    completedAt = prop.Value.GetDateTimeOffset("O");
                    continue;
                }
                if (prop.NameEquals("duration_ms"u8))
                {
                    if (prop.Value.ValueKind == JsonValueKind.Null)
                    {
                        continue;
                    }
                    durationMs = prop.Value.GetInt32();
                    continue;
                }
                if (prop.NameEquals("created_at"u8))
                {
                    createdAt = prop.Value.GetDateTimeOffset("O");
                    continue;
                }
                if (options.Format != "W")
                {
                    additionalBinaryDataProperties.Add(prop.Name, BinaryData.FromString(prop.Value.GetRawText()));
                }
            }
            return new WorkflowNodeEntity(
                id,
                runId,
                nodeId,
                nodeType,
                nodeName,
                attempt,
                inputJson,
                outputJson,
                status,
                errorMessage,
                retryCount,
                maxRetries,
                timeoutMs,
                startedAt,
                completedAt,
                durationMs,
                createdAt,
                additionalBinaryDataProperties);
        }
    }
}
