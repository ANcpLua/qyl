
#nullable disable

using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Text.Json;
using Qyl.Client;

namespace Qyl.Domains.Configurator
{
    public partial class GenerationJobEntity : IJsonModel<GenerationJobEntity>
    {
        internal GenerationJobEntity()
        {
        }

        protected virtual GenerationJobEntity PersistableModelCreateCore(BinaryData data, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<GenerationJobEntity>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    using (JsonDocument document = JsonDocument.Parse(data, ModelSerializationExtensions.JsonDocumentOptions))
                    {
                        return DeserializeGenerationJobEntity(document.RootElement, options);
                    }
                default:
                    throw new FormatException($"The model {nameof(GenerationJobEntity)} does not support reading '{options.Format}' format.");
            }
        }

        protected virtual BinaryData PersistableModelWriteCore(ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<GenerationJobEntity>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    return ModelReaderWriter.Write(this, options, QylClientContext.Default);
                default:
                    throw new FormatException($"The model {nameof(GenerationJobEntity)} does not support writing '{options.Format}' format.");
            }
        }

        BinaryData IPersistableModel<GenerationJobEntity>.Write(ModelReaderWriterOptions options) => PersistableModelWriteCore(options);

        GenerationJobEntity IPersistableModel<GenerationJobEntity>.Create(BinaryData data, ModelReaderWriterOptions options) => PersistableModelCreateCore(data, options);

        string IPersistableModel<GenerationJobEntity>.GetFormatFromOptions(ModelReaderWriterOptions options) => "J";

        public static explicit operator GenerationJobEntity(ClientResult result)
        {
            PipelineResponse response = result.GetRawResponse();
            using JsonDocument document = JsonDocument.Parse(response.Content, ModelSerializationExtensions.JsonDocumentOptions);
            return DeserializeGenerationJobEntity(document.RootElement, ModelSerializationExtensions.WireOptions);
        }

        void IJsonModel<GenerationJobEntity>.Write(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            writer.WriteStartObject();
            JsonModelWriteCore(writer, options);
            writer.WriteEndObject();
        }

        protected virtual void JsonModelWriteCore(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<GenerationJobEntity>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(GenerationJobEntity)} does not support writing '{format}' format.");
            }
            writer.WritePropertyName("id"u8);
            writer.WriteStringValue(Id);
            writer.WritePropertyName("workspace_id"u8);
            writer.WriteStringValue(WorkspaceId);
            writer.WritePropertyName("profile_id"u8);
            writer.WriteStringValue(ProfileId);
            writer.WritePropertyName("job_type"u8);
            writer.WriteStringValue(JobType.ToSerialString());
            writer.WritePropertyName("status"u8);
            writer.WriteStringValue(Status.ToSerialString());
            writer.WritePropertyName("priority"u8);
            writer.WriteNumberValue(Priority);
            if (Optional.IsDefined(InputHash))
            {
                writer.WritePropertyName("input_hash"u8);
                writer.WriteStringValue(InputHash);
            }
            if (Optional.IsDefined(OutputPath))
            {
                writer.WritePropertyName("output_path"u8);
                writer.WriteStringValue(OutputPath);
            }
            if (Optional.IsDefined(OutputHash))
            {
                writer.WritePropertyName("output_hash"u8);
                writer.WriteStringValue(OutputHash);
            }
            if (Optional.IsDefined(ErrorMessage))
            {
                writer.WritePropertyName("error_message"u8);
                writer.WriteStringValue(ErrorMessage);
            }
            writer.WritePropertyName("queued_at"u8);
            writer.WriteStringValue(QueuedAt, "O");
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

        GenerationJobEntity IJsonModel<GenerationJobEntity>.Create(ref Utf8JsonReader reader, ModelReaderWriterOptions options) => JsonModelCreateCore(ref reader, options);

        protected virtual GenerationJobEntity JsonModelCreateCore(ref Utf8JsonReader reader, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<GenerationJobEntity>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(GenerationJobEntity)} does not support reading '{format}' format.");
            }
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            return DeserializeGenerationJobEntity(document.RootElement, options);
        }

        internal static GenerationJobEntity DeserializeGenerationJobEntity(JsonElement element, ModelReaderWriterOptions options)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }
            string id = default;
            string workspaceId = default;
            string profileId = default;
            GenerationJobType jobType = default;
            JobStatus status = default;
            int priority = default;
            string inputHash = default;
            string outputPath = default;
            string outputHash = default;
            string errorMessage = default;
            DateTimeOffset queuedAt = default;
            DateTimeOffset? startedAt = default;
            DateTimeOffset? completedAt = default;
            int? durationMs = default;
            IDictionary<string, BinaryData> additionalBinaryDataProperties = new ChangeTrackingDictionary<string, BinaryData>();
            foreach (var prop in element.EnumerateObject())
            {
                if (prop.NameEquals("id"u8))
                {
                    id = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("workspace_id"u8))
                {
                    workspaceId = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("profile_id"u8))
                {
                    profileId = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("job_type"u8))
                {
                    jobType = prop.Value.GetString().ToGenerationJobType();
                    continue;
                }
                if (prop.NameEquals("status"u8))
                {
                    status = prop.Value.GetString().ToJobStatus();
                    continue;
                }
                if (prop.NameEquals("priority"u8))
                {
                    priority = prop.Value.GetInt32();
                    continue;
                }
                if (prop.NameEquals("input_hash"u8))
                {
                    inputHash = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("output_path"u8))
                {
                    outputPath = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("output_hash"u8))
                {
                    outputHash = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("error_message"u8))
                {
                    errorMessage = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("queued_at"u8))
                {
                    queuedAt = prop.Value.GetDateTimeOffset("O");
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
                if (options.Format != "W")
                {
                    additionalBinaryDataProperties.Add(prop.Name, BinaryData.FromString(prop.Value.GetRawText()));
                }
            }
            return new GenerationJobEntity(
                id,
                workspaceId,
                profileId,
                jobType,
                status,
                priority,
                inputHash,
                outputPath,
                outputHash,
                errorMessage,
                queuedAt,
                startedAt,
                completedAt,
                durationMs,
                additionalBinaryDataProperties);
        }
    }
}
