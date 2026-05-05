
#nullable disable

using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Text.Json;
using Qyl.Client;

namespace Qyl.Domains.Workflow
{
    public partial class WorkflowRunEntity : IJsonModel<WorkflowRunEntity>
    {
        internal WorkflowRunEntity()
        {
        }

        protected virtual WorkflowRunEntity PersistableModelCreateCore(BinaryData data, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<WorkflowRunEntity>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    using (JsonDocument document = JsonDocument.Parse(data, ModelSerializationExtensions.JsonDocumentOptions))
                    {
                        return DeserializeWorkflowRunEntity(document.RootElement, options);
                    }
                default:
                    throw new FormatException($"The model {nameof(WorkflowRunEntity)} does not support reading '{options.Format}' format.");
            }
        }

        protected virtual BinaryData PersistableModelWriteCore(ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<WorkflowRunEntity>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    return ModelReaderWriter.Write(this, options, QylClientContext.Default);
                default:
                    throw new FormatException($"The model {nameof(WorkflowRunEntity)} does not support writing '{options.Format}' format.");
            }
        }

        BinaryData IPersistableModel<WorkflowRunEntity>.Write(ModelReaderWriterOptions options) => PersistableModelWriteCore(options);

        WorkflowRunEntity IPersistableModel<WorkflowRunEntity>.Create(BinaryData data, ModelReaderWriterOptions options) => PersistableModelCreateCore(data, options);

        string IPersistableModel<WorkflowRunEntity>.GetFormatFromOptions(ModelReaderWriterOptions options) => "J";

        public static explicit operator WorkflowRunEntity(ClientResult result)
        {
            PipelineResponse response = result.GetRawResponse();
            using JsonDocument document = JsonDocument.Parse(response.Content, ModelSerializationExtensions.JsonDocumentOptions);
            return DeserializeWorkflowRunEntity(document.RootElement, ModelSerializationExtensions.WireOptions);
        }

        void IJsonModel<WorkflowRunEntity>.Write(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            writer.WriteStartObject();
            JsonModelWriteCore(writer, options);
            writer.WriteEndObject();
        }

        protected virtual void JsonModelWriteCore(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<WorkflowRunEntity>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(WorkflowRunEntity)} does not support writing '{format}' format.");
            }
            writer.WritePropertyName("id"u8);
            writer.WriteStringValue(Id);
            writer.WritePropertyName("workflow_id"u8);
            writer.WriteStringValue(WorkflowId);
            writer.WritePropertyName("workflow_version"u8);
            writer.WriteNumberValue(WorkflowVersion);
            writer.WritePropertyName("project_id"u8);
            writer.WriteStringValue(ProjectId);
            writer.WritePropertyName("trigger_type"u8);
            writer.WriteStringValue(TriggerType.ToSerialString());
            if (Optional.IsDefined(TriggerSource))
            {
                writer.WritePropertyName("trigger_source"u8);
                writer.WriteStringValue(TriggerSource);
            }
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
            if (Optional.IsDefined(ParentRunId))
            {
                writer.WritePropertyName("parent_run_id"u8);
                writer.WriteStringValue(ParentRunId);
            }
            if (Optional.IsDefined(CorrelationId))
            {
                writer.WritePropertyName("correlation_id"u8);
                writer.WriteStringValue(CorrelationId);
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

        WorkflowRunEntity IJsonModel<WorkflowRunEntity>.Create(ref Utf8JsonReader reader, ModelReaderWriterOptions options) => JsonModelCreateCore(ref reader, options);

        protected virtual WorkflowRunEntity JsonModelCreateCore(ref Utf8JsonReader reader, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<WorkflowRunEntity>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(WorkflowRunEntity)} does not support reading '{format}' format.");
            }
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            return DeserializeWorkflowRunEntity(document.RootElement, options);
        }

        internal static WorkflowRunEntity DeserializeWorkflowRunEntity(JsonElement element, ModelReaderWriterOptions options)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }
            string id = default;
            string workflowId = default;
            int workflowVersion = default;
            string projectId = default;
            WorkflowTriggerType triggerType = default;
            string triggerSource = default;
            string inputJson = default;
            string outputJson = default;
            WorkflowRunStatus status = default;
            string errorMessage = default;
            string parentRunId = default;
            string correlationId = default;
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
                if (prop.NameEquals("workflow_id"u8))
                {
                    workflowId = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("workflow_version"u8))
                {
                    workflowVersion = prop.Value.GetInt32();
                    continue;
                }
                if (prop.NameEquals("project_id"u8))
                {
                    projectId = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("trigger_type"u8))
                {
                    triggerType = prop.Value.GetString().ToWorkflowTriggerType();
                    continue;
                }
                if (prop.NameEquals("trigger_source"u8))
                {
                    triggerSource = prop.Value.GetString();
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
                if (prop.NameEquals("parent_run_id"u8))
                {
                    parentRunId = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("correlation_id"u8))
                {
                    correlationId = prop.Value.GetString();
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
            return new WorkflowRunEntity(
                id,
                workflowId,
                workflowVersion,
                projectId,
                triggerType,
                triggerSource,
                inputJson,
                outputJson,
                status,
                errorMessage,
                parentRunId,
                correlationId,
                startedAt,
                completedAt,
                durationMs,
                createdAt,
                additionalBinaryDataProperties);
        }
    }
}
