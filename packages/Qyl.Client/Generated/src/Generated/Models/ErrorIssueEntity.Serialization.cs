
#nullable disable

using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Text.Json;
using Qyl.Client;

namespace Qyl.Domains.Issues
{
    public partial class ErrorIssueEntity : IJsonModel<ErrorIssueEntity>
    {
        internal ErrorIssueEntity()
        {
        }

        protected virtual ErrorIssueEntity PersistableModelCreateCore(BinaryData data, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<ErrorIssueEntity>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    using (JsonDocument document = JsonDocument.Parse(data, ModelSerializationExtensions.JsonDocumentOptions))
                    {
                        return DeserializeErrorIssueEntity(document.RootElement, options);
                    }
                default:
                    throw new FormatException($"The model {nameof(ErrorIssueEntity)} does not support reading '{options.Format}' format.");
            }
        }

        protected virtual BinaryData PersistableModelWriteCore(ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<ErrorIssueEntity>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    return ModelReaderWriter.Write(this, options, QylClientContext.Default);
                default:
                    throw new FormatException($"The model {nameof(ErrorIssueEntity)} does not support writing '{options.Format}' format.");
            }
        }

        BinaryData IPersistableModel<ErrorIssueEntity>.Write(ModelReaderWriterOptions options) => PersistableModelWriteCore(options);

        ErrorIssueEntity IPersistableModel<ErrorIssueEntity>.Create(BinaryData data, ModelReaderWriterOptions options) => PersistableModelCreateCore(data, options);

        string IPersistableModel<ErrorIssueEntity>.GetFormatFromOptions(ModelReaderWriterOptions options) => "J";

        public static explicit operator ErrorIssueEntity(ClientResult result)
        {
            PipelineResponse response = result.GetRawResponse();
            using JsonDocument document = JsonDocument.Parse(response.Content, ModelSerializationExtensions.JsonDocumentOptions);
            return DeserializeErrorIssueEntity(document.RootElement, ModelSerializationExtensions.WireOptions);
        }

        void IJsonModel<ErrorIssueEntity>.Write(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            writer.WriteStartObject();
            JsonModelWriteCore(writer, options);
            writer.WriteEndObject();
        }

        protected virtual void JsonModelWriteCore(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<ErrorIssueEntity>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(ErrorIssueEntity)} does not support writing '{format}' format.");
            }
            writer.WritePropertyName("id"u8);
            writer.WriteStringValue(Id);
            writer.WritePropertyName("project_id"u8);
            writer.WriteStringValue(ProjectId);
            writer.WritePropertyName("fingerprint"u8);
            writer.WriteStringValue(Fingerprint);
            writer.WritePropertyName("title"u8);
            writer.WriteStringValue(Title);
            if (Optional.IsDefined(Culprit))
            {
                writer.WritePropertyName("culprit"u8);
                writer.WriteStringValue(Culprit);
            }
            writer.WritePropertyName("error_type"u8);
            writer.WriteStringValue(ErrorType);
            writer.WritePropertyName("category"u8);
            writer.WriteStringValue(Category);
            writer.WritePropertyName("level"u8);
            writer.WriteStringValue(Level.ToSerialString());
            if (Optional.IsDefined(Platform))
            {
                writer.WritePropertyName("platform"u8);
                writer.WriteStringValue(Platform);
            }
            writer.WritePropertyName("first_seen_at"u8);
            writer.WriteStringValue(FirstSeenAt, "O");
            writer.WritePropertyName("last_seen_at"u8);
            writer.WriteStringValue(LastSeenAt, "O");
            writer.WritePropertyName("occurrence_count"u8);
            writer.WriteNumberValue(OccurrenceCount);
            writer.WritePropertyName("affected_users_count"u8);
            writer.WriteNumberValue(AffectedUsersCount);
            writer.WritePropertyName("status"u8);
            writer.WriteStringValue(Status.ToSerialString());
            if (Optional.IsDefined(Substatus))
            {
                writer.WritePropertyName("substatus"u8);
                writer.WriteStringValue(Substatus);
            }
            writer.WritePropertyName("priority"u8);
            writer.WriteStringValue(Priority.ToSerialString());
            if (Optional.IsDefined(AssignedTo))
            {
                writer.WritePropertyName("assigned_to"u8);
                writer.WriteStringValue(AssignedTo);
            }
            if (Optional.IsDefined(ResolvedAt))
            {
                writer.WritePropertyName("resolved_at"u8);
                writer.WriteStringValue(ResolvedAt.Value, "O");
            }
            if (Optional.IsDefined(ResolvedBy))
            {
                writer.WritePropertyName("resolved_by"u8);
                writer.WriteStringValue(ResolvedBy);
            }
            writer.WritePropertyName("regression_count"u8);
            writer.WriteNumberValue(RegressionCount);
            if (Optional.IsDefined(LastRelease))
            {
                writer.WritePropertyName("last_release"u8);
                writer.WriteStringValue(LastRelease);
            }
            if (Optional.IsDefined(TagsJson))
            {
                writer.WritePropertyName("tags_json"u8);
                writer.WriteStringValue(TagsJson);
            }
            if (Optional.IsDefined(MetadataJson))
            {
                writer.WritePropertyName("metadata_json"u8);
                writer.WriteStringValue(MetadataJson);
            }
            writer.WritePropertyName("created_at"u8);
            writer.WriteStringValue(CreatedAt, "O");
            writer.WritePropertyName("updated_at"u8);
            writer.WriteStringValue(UpdatedAt, "O");
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

        ErrorIssueEntity IJsonModel<ErrorIssueEntity>.Create(ref Utf8JsonReader reader, ModelReaderWriterOptions options) => JsonModelCreateCore(ref reader, options);

        protected virtual ErrorIssueEntity JsonModelCreateCore(ref Utf8JsonReader reader, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<ErrorIssueEntity>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(ErrorIssueEntity)} does not support reading '{format}' format.");
            }
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            return DeserializeErrorIssueEntity(document.RootElement, options);
        }

        internal static ErrorIssueEntity DeserializeErrorIssueEntity(JsonElement element, ModelReaderWriterOptions options)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }
            string id = default;
            string projectId = default;
            string fingerprint = default;
            string title = default;
            string culprit = default;
            string errorType = default;
            string category = default;
            IssueLevel level = default;
            string platform = default;
            DateTimeOffset firstSeenAt = default;
            DateTimeOffset lastSeenAt = default;
            long occurrenceCount = default;
            int affectedUsersCount = default;
            IssueStatus status = default;
            string substatus = default;
            IssuePriority priority = default;
            string assignedTo = default;
            DateTimeOffset? resolvedAt = default;
            string resolvedBy = default;
            int regressionCount = default;
            string lastRelease = default;
            string tagsJson = default;
            string metadataJson = default;
            DateTimeOffset createdAt = default;
            DateTimeOffset updatedAt = default;
            IDictionary<string, BinaryData> additionalBinaryDataProperties = new ChangeTrackingDictionary<string, BinaryData>();
            foreach (var prop in element.EnumerateObject())
            {
                if (prop.NameEquals("id"u8))
                {
                    id = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("project_id"u8))
                {
                    projectId = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("fingerprint"u8))
                {
                    fingerprint = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("title"u8))
                {
                    title = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("culprit"u8))
                {
                    culprit = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("error_type"u8))
                {
                    errorType = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("category"u8))
                {
                    category = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("level"u8))
                {
                    level = prop.Value.GetString().ToIssueLevel();
                    continue;
                }
                if (prop.NameEquals("platform"u8))
                {
                    platform = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("first_seen_at"u8))
                {
                    firstSeenAt = prop.Value.GetDateTimeOffset("O");
                    continue;
                }
                if (prop.NameEquals("last_seen_at"u8))
                {
                    lastSeenAt = prop.Value.GetDateTimeOffset("O");
                    continue;
                }
                if (prop.NameEquals("occurrence_count"u8))
                {
                    occurrenceCount = prop.Value.GetInt64();
                    continue;
                }
                if (prop.NameEquals("affected_users_count"u8))
                {
                    affectedUsersCount = prop.Value.GetInt32();
                    continue;
                }
                if (prop.NameEquals("status"u8))
                {
                    status = prop.Value.GetString().ToIssueStatus();
                    continue;
                }
                if (prop.NameEquals("substatus"u8))
                {
                    substatus = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("priority"u8))
                {
                    priority = prop.Value.GetString().ToIssuePriority();
                    continue;
                }
                if (prop.NameEquals("assigned_to"u8))
                {
                    assignedTo = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("resolved_at"u8))
                {
                    if (prop.Value.ValueKind == JsonValueKind.Null)
                    {
                        continue;
                    }
                    resolvedAt = prop.Value.GetDateTimeOffset("O");
                    continue;
                }
                if (prop.NameEquals("resolved_by"u8))
                {
                    resolvedBy = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("regression_count"u8))
                {
                    regressionCount = prop.Value.GetInt32();
                    continue;
                }
                if (prop.NameEquals("last_release"u8))
                {
                    lastRelease = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("tags_json"u8))
                {
                    tagsJson = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("metadata_json"u8))
                {
                    metadataJson = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("created_at"u8))
                {
                    createdAt = prop.Value.GetDateTimeOffset("O");
                    continue;
                }
                if (prop.NameEquals("updated_at"u8))
                {
                    updatedAt = prop.Value.GetDateTimeOffset("O");
                    continue;
                }
                if (options.Format != "W")
                {
                    additionalBinaryDataProperties.Add(prop.Name, BinaryData.FromString(prop.Value.GetRawText()));
                }
            }
            return new ErrorIssueEntity(
                id,
                projectId,
                fingerprint,
                title,
                culprit,
                errorType,
                category,
                level,
                platform,
                firstSeenAt,
                lastSeenAt,
                occurrenceCount,
                affectedUsersCount,
                status,
                substatus,
                priority,
                assignedTo,
                resolvedAt,
                resolvedBy,
                regressionCount,
                lastRelease,
                tagsJson,
                metadataJson,
                createdAt,
                updatedAt,
                additionalBinaryDataProperties);
        }
    }
}
