
#nullable disable

using System;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Text.Json;
using Qyl.Client;

namespace Qyl.Domains.Identity
{
    public partial class ServiceDependency : IJsonModel<ServiceDependency>
    {
        internal ServiceDependency()
        {
        }

        protected virtual ServiceDependency PersistableModelCreateCore(BinaryData data, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<ServiceDependency>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    using (JsonDocument document = JsonDocument.Parse(data, ModelSerializationExtensions.JsonDocumentOptions))
                    {
                        return DeserializeServiceDependency(document.RootElement, options);
                    }
                default:
                    throw new FormatException($"The model {nameof(ServiceDependency)} does not support reading '{options.Format}' format.");
            }
        }

        protected virtual BinaryData PersistableModelWriteCore(ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<ServiceDependency>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    return ModelReaderWriter.Write(this, options, QylClientContext.Default);
                default:
                    throw new FormatException($"The model {nameof(ServiceDependency)} does not support writing '{options.Format}' format.");
            }
        }

        BinaryData IPersistableModel<ServiceDependency>.Write(ModelReaderWriterOptions options) => PersistableModelWriteCore(options);

        ServiceDependency IPersistableModel<ServiceDependency>.Create(BinaryData data, ModelReaderWriterOptions options) => PersistableModelCreateCore(data, options);

        string IPersistableModel<ServiceDependency>.GetFormatFromOptions(ModelReaderWriterOptions options) => "J";

        void IJsonModel<ServiceDependency>.Write(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            writer.WriteStartObject();
            JsonModelWriteCore(writer, options);
            writer.WriteEndObject();
        }

        protected virtual void JsonModelWriteCore(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<ServiceDependency>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(ServiceDependency)} does not support writing '{format}' format.");
            }
            writer.WritePropertyName("source_service"u8);
            writer.WriteStringValue(SourceService);
            writer.WritePropertyName("target_service"u8);
            writer.WriteStringValue(TargetService);
            writer.WritePropertyName("request_count"u8);
            writer.WriteNumberValue(RequestCount);
            writer.WritePropertyName("error_rate"u8);
            writer.WriteNumberValue(ErrorRate);
            writer.WritePropertyName("avg_latency_ms"u8);
            writer.WriteNumberValue(AvgLatencyMs);
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

        ServiceDependency IJsonModel<ServiceDependency>.Create(ref Utf8JsonReader reader, ModelReaderWriterOptions options) => JsonModelCreateCore(ref reader, options);

        protected virtual ServiceDependency JsonModelCreateCore(ref Utf8JsonReader reader, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<ServiceDependency>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(ServiceDependency)} does not support reading '{format}' format.");
            }
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            return DeserializeServiceDependency(document.RootElement, options);
        }

        internal static ServiceDependency DeserializeServiceDependency(JsonElement element, ModelReaderWriterOptions options)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }
            string sourceService = default;
            string targetService = default;
            long requestCount = default;
            double errorRate = default;
            double avgLatencyMs = default;
            IDictionary<string, BinaryData> additionalBinaryDataProperties = new ChangeTrackingDictionary<string, BinaryData>();
            foreach (var prop in element.EnumerateObject())
            {
                if (prop.NameEquals("source_service"u8))
                {
                    sourceService = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("target_service"u8))
                {
                    targetService = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("request_count"u8))
                {
                    requestCount = prop.Value.GetInt64();
                    continue;
                }
                if (prop.NameEquals("error_rate"u8))
                {
                    errorRate = prop.Value.GetDouble();
                    continue;
                }
                if (prop.NameEquals("avg_latency_ms"u8))
                {
                    avgLatencyMs = prop.Value.GetDouble();
                    continue;
                }
                if (options.Format != "W")
                {
                    additionalBinaryDataProperties.Add(prop.Name, BinaryData.FromString(prop.Value.GetRawText()));
                }
            }
            return new ServiceDependency(
                sourceService,
                targetService,
                requestCount,
                errorRate,
                avgLatencyMs,
                additionalBinaryDataProperties);
        }
    }
}
