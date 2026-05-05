
#nullable disable

using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Text.Json;
using Qyl.Client;

namespace Qyl.Domains.Alerting
{
    public partial class AlertRuleEntity : IJsonModel<AlertRuleEntity>
    {
        internal AlertRuleEntity()
        {
        }

        protected virtual AlertRuleEntity PersistableModelCreateCore(BinaryData data, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<AlertRuleEntity>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    using (JsonDocument document = JsonDocument.Parse(data, ModelSerializationExtensions.JsonDocumentOptions))
                    {
                        return DeserializeAlertRuleEntity(document.RootElement, options);
                    }
                default:
                    throw new FormatException($"The model {nameof(AlertRuleEntity)} does not support reading '{options.Format}' format.");
            }
        }

        protected virtual BinaryData PersistableModelWriteCore(ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<AlertRuleEntity>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    return ModelReaderWriter.Write(this, options, QylClientContext.Default);
                default:
                    throw new FormatException($"The model {nameof(AlertRuleEntity)} does not support writing '{options.Format}' format.");
            }
        }

        BinaryData IPersistableModel<AlertRuleEntity>.Write(ModelReaderWriterOptions options) => PersistableModelWriteCore(options);

        AlertRuleEntity IPersistableModel<AlertRuleEntity>.Create(BinaryData data, ModelReaderWriterOptions options) => PersistableModelCreateCore(data, options);

        string IPersistableModel<AlertRuleEntity>.GetFormatFromOptions(ModelReaderWriterOptions options) => "J";

        public static implicit operator BinaryContent(AlertRuleEntity alertRuleEntity)
        {
            if (alertRuleEntity == null)
            {
                return null;
            }
            return BinaryContent.Create(alertRuleEntity, ModelSerializationExtensions.WireOptions);
        }

        public static explicit operator AlertRuleEntity(ClientResult result)
        {
            PipelineResponse response = result.GetRawResponse();
            using JsonDocument document = JsonDocument.Parse(response.Content, ModelSerializationExtensions.JsonDocumentOptions);
            return DeserializeAlertRuleEntity(document.RootElement, ModelSerializationExtensions.WireOptions);
        }

        void IJsonModel<AlertRuleEntity>.Write(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            writer.WriteStartObject();
            JsonModelWriteCore(writer, options);
            writer.WriteEndObject();
        }

        protected virtual void JsonModelWriteCore(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<AlertRuleEntity>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(AlertRuleEntity)} does not support writing '{format}' format.");
            }
            writer.WritePropertyName("id"u8);
            writer.WriteStringValue(Id);
            writer.WritePropertyName("project_id"u8);
            writer.WriteStringValue(ProjectId);
            writer.WritePropertyName("name"u8);
            writer.WriteStringValue(Name);
            if (Optional.IsDefined(Description))
            {
                writer.WritePropertyName("description"u8);
                writer.WriteStringValue(Description);
            }
            writer.WritePropertyName("rule_type"u8);
            writer.WriteStringValue(RuleType.ToSerialString());
            writer.WritePropertyName("condition_json"u8);
            writer.WriteStringValue(ConditionJson);
            if (Optional.IsDefined(ThresholdJson))
            {
                writer.WritePropertyName("threshold_json"u8);
                writer.WriteStringValue(ThresholdJson);
            }
            writer.WritePropertyName("target_type"u8);
            writer.WriteStringValue(TargetType);
            if (Optional.IsDefined(TargetFilterJson))
            {
                writer.WritePropertyName("target_filter_json"u8);
                writer.WriteStringValue(TargetFilterJson);
            }
            writer.WritePropertyName("severity"u8);
            writer.WriteStringValue(Severity.ToSerialString());
            writer.WritePropertyName("cooldown_seconds"u8);
            writer.WriteNumberValue(CooldownSeconds);
            if (Optional.IsDefined(NotificationChannelsJson))
            {
                writer.WritePropertyName("notification_channels_json"u8);
                writer.WriteStringValue(NotificationChannelsJson);
            }
            writer.WritePropertyName("enabled"u8);
            writer.WriteBooleanValue(Enabled);
            if (Optional.IsDefined(LastTriggeredAt))
            {
                writer.WritePropertyName("last_triggered_at"u8);
                writer.WriteStringValue(LastTriggeredAt.Value, "O");
            }
            writer.WritePropertyName("trigger_count"u8);
            writer.WriteNumberValue(TriggerCount);
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

        AlertRuleEntity IJsonModel<AlertRuleEntity>.Create(ref Utf8JsonReader reader, ModelReaderWriterOptions options) => JsonModelCreateCore(ref reader, options);

        protected virtual AlertRuleEntity JsonModelCreateCore(ref Utf8JsonReader reader, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<AlertRuleEntity>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(AlertRuleEntity)} does not support reading '{format}' format.");
            }
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            return DeserializeAlertRuleEntity(document.RootElement, options);
        }

        internal static AlertRuleEntity DeserializeAlertRuleEntity(JsonElement element, ModelReaderWriterOptions options)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }
            string id = default;
            string projectId = default;
            string name = default;
            string description = default;
            AlertRuleType ruleType = default;
            string conditionJson = default;
            string thresholdJson = default;
            string targetType = default;
            string targetFilterJson = default;
            AlertSeverity severity = default;
            int cooldownSeconds = default;
            string notificationChannelsJson = default;
            bool enabled = default;
            DateTimeOffset? lastTriggeredAt = default;
            long triggerCount = default;
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
                if (prop.NameEquals("name"u8))
                {
                    name = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("description"u8))
                {
                    description = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("rule_type"u8))
                {
                    ruleType = prop.Value.GetString().ToAlertRuleType();
                    continue;
                }
                if (prop.NameEquals("condition_json"u8))
                {
                    conditionJson = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("threshold_json"u8))
                {
                    thresholdJson = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("target_type"u8))
                {
                    targetType = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("target_filter_json"u8))
                {
                    targetFilterJson = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("severity"u8))
                {
                    severity = prop.Value.GetString().ToAlertSeverity();
                    continue;
                }
                if (prop.NameEquals("cooldown_seconds"u8))
                {
                    cooldownSeconds = prop.Value.GetInt32();
                    continue;
                }
                if (prop.NameEquals("notification_channels_json"u8))
                {
                    notificationChannelsJson = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("enabled"u8))
                {
                    enabled = prop.Value.GetBoolean();
                    continue;
                }
                if (prop.NameEquals("last_triggered_at"u8))
                {
                    if (prop.Value.ValueKind == JsonValueKind.Null)
                    {
                        continue;
                    }
                    lastTriggeredAt = prop.Value.GetDateTimeOffset("O");
                    continue;
                }
                if (prop.NameEquals("trigger_count"u8))
                {
                    triggerCount = prop.Value.GetInt64();
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
            return new AlertRuleEntity(
                id,
                projectId,
                name,
                description,
                ruleType,
                conditionJson,
                thresholdJson,
                targetType,
                targetFilterJson,
                severity,
                cooldownSeconds,
                notificationChannelsJson,
                enabled,
                lastTriggeredAt,
                triggerCount,
                createdAt,
                updatedAt,
                additionalBinaryDataProperties);
        }
    }
}
