
#nullable disable

using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Text.Json;
using Qyl.Client;

namespace Qyl.Api
{
    public partial class DoraMetrics : IJsonModel<DoraMetrics>
    {
        internal DoraMetrics()
        {
        }

        protected virtual DoraMetrics PersistableModelCreateCore(BinaryData data, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<DoraMetrics>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    using (JsonDocument document = JsonDocument.Parse(data, ModelSerializationExtensions.JsonDocumentOptions))
                    {
                        return DeserializeDoraMetrics(document.RootElement, options);
                    }
                default:
                    throw new FormatException($"The model {nameof(DoraMetrics)} does not support reading '{options.Format}' format.");
            }
        }

        protected virtual BinaryData PersistableModelWriteCore(ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<DoraMetrics>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    return ModelReaderWriter.Write(this, options, QylClientContext.Default);
                default:
                    throw new FormatException($"The model {nameof(DoraMetrics)} does not support writing '{options.Format}' format.");
            }
        }

        BinaryData IPersistableModel<DoraMetrics>.Write(ModelReaderWriterOptions options) => PersistableModelWriteCore(options);

        DoraMetrics IPersistableModel<DoraMetrics>.Create(BinaryData data, ModelReaderWriterOptions options) => PersistableModelCreateCore(data, options);

        string IPersistableModel<DoraMetrics>.GetFormatFromOptions(ModelReaderWriterOptions options) => "J";

        public static explicit operator DoraMetrics(ClientResult result)
        {
            PipelineResponse response = result.GetRawResponse();
            using JsonDocument document = JsonDocument.Parse(response.Content, ModelSerializationExtensions.JsonDocumentOptions);
            return DeserializeDoraMetrics(document.RootElement, ModelSerializationExtensions.WireOptions);
        }

        void IJsonModel<DoraMetrics>.Write(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            writer.WriteStartObject();
            JsonModelWriteCore(writer, options);
            writer.WriteEndObject();
        }

        protected virtual void JsonModelWriteCore(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<DoraMetrics>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(DoraMetrics)} does not support writing '{format}' format.");
            }
            writer.WritePropertyName("deployment_frequency"u8);
            writer.WriteNumberValue(DeploymentFrequency);
            writer.WritePropertyName("lead_time_hours"u8);
            writer.WriteNumberValue(LeadTimeHours);
            writer.WritePropertyName("change_failure_rate"u8);
            writer.WriteNumberValue(ChangeFailureRate);
            writer.WritePropertyName("mttr_hours"u8);
            writer.WriteNumberValue(MttrHours);
            writer.WritePropertyName("performance_level"u8);
            writer.WriteStringValue(PerformanceLevel.ToSerialString());
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

        DoraMetrics IJsonModel<DoraMetrics>.Create(ref Utf8JsonReader reader, ModelReaderWriterOptions options) => JsonModelCreateCore(ref reader, options);

        protected virtual DoraMetrics JsonModelCreateCore(ref Utf8JsonReader reader, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<DoraMetrics>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(DoraMetrics)} does not support reading '{format}' format.");
            }
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            return DeserializeDoraMetrics(document.RootElement, options);
        }

        internal static DoraMetrics DeserializeDoraMetrics(JsonElement element, ModelReaderWriterOptions options)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }
            double deploymentFrequency = default;
            double leadTimeHours = default;
            double changeFailureRate = default;
            double mttrHours = default;
            DoraPerformanceLevel performanceLevel = default;
            IDictionary<string, BinaryData> additionalBinaryDataProperties = new ChangeTrackingDictionary<string, BinaryData>();
            foreach (var prop in element.EnumerateObject())
            {
                if (prop.NameEquals("deployment_frequency"u8))
                {
                    deploymentFrequency = prop.Value.GetDouble();
                    continue;
                }
                if (prop.NameEquals("lead_time_hours"u8))
                {
                    leadTimeHours = prop.Value.GetDouble();
                    continue;
                }
                if (prop.NameEquals("change_failure_rate"u8))
                {
                    changeFailureRate = prop.Value.GetDouble();
                    continue;
                }
                if (prop.NameEquals("mttr_hours"u8))
                {
                    mttrHours = prop.Value.GetDouble();
                    continue;
                }
                if (prop.NameEquals("performance_level"u8))
                {
                    performanceLevel = prop.Value.GetString().ToDoraPerformanceLevel();
                    continue;
                }
                if (options.Format != "W")
                {
                    additionalBinaryDataProperties.Add(prop.Name, BinaryData.FromString(prop.Value.GetRawText()));
                }
            }
            return new DoraMetrics(
                deploymentFrequency,
                leadTimeHours,
                changeFailureRate,
                mttrHours,
                performanceLevel,
                additionalBinaryDataProperties);
        }
    }
}
