
#nullable disable

using System;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Text.Json;
using Qyl.Client;

namespace Qyl.Domains.Observe.Session
{
    public partial class SessionGenAiUsage : IJsonModel<SessionGenAiUsage>
    {
        internal SessionGenAiUsage()
        {
        }

        protected virtual SessionGenAiUsage PersistableModelCreateCore(BinaryData data, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<SessionGenAiUsage>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    using (JsonDocument document = JsonDocument.Parse(data, ModelSerializationExtensions.JsonDocumentOptions))
                    {
                        return DeserializeSessionGenAiUsage(document.RootElement, options);
                    }
                default:
                    throw new FormatException($"The model {nameof(SessionGenAiUsage)} does not support reading '{options.Format}' format.");
            }
        }

        protected virtual BinaryData PersistableModelWriteCore(ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<SessionGenAiUsage>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    return ModelReaderWriter.Write(this, options, QylClientContext.Default);
                default:
                    throw new FormatException($"The model {nameof(SessionGenAiUsage)} does not support writing '{options.Format}' format.");
            }
        }

        BinaryData IPersistableModel<SessionGenAiUsage>.Write(ModelReaderWriterOptions options) => PersistableModelWriteCore(options);

        SessionGenAiUsage IPersistableModel<SessionGenAiUsage>.Create(BinaryData data, ModelReaderWriterOptions options) => PersistableModelCreateCore(data, options);

        string IPersistableModel<SessionGenAiUsage>.GetFormatFromOptions(ModelReaderWriterOptions options) => "J";

        void IJsonModel<SessionGenAiUsage>.Write(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            writer.WriteStartObject();
            JsonModelWriteCore(writer, options);
            writer.WriteEndObject();
        }

        protected virtual void JsonModelWriteCore(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<SessionGenAiUsage>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(SessionGenAiUsage)} does not support writing '{format}' format.");
            }
            writer.WritePropertyName("request_count"u8);
            writer.WriteNumberValue(RequestCount);
            writer.WritePropertyName("total_input_tokens"u8);
            writer.WriteNumberValue(TotalInputTokens);
            writer.WritePropertyName("total_output_tokens"u8);
            writer.WriteNumberValue(TotalOutputTokens);
            writer.WritePropertyName("models_used"u8);
            writer.WriteStartArray();
            foreach (string item in ModelsUsed)
            {
                if (item == null)
                {
                    writer.WriteNullValue();
                    continue;
                }
                writer.WriteStringValue(item);
            }
            writer.WriteEndArray();
            writer.WritePropertyName("providers_used"u8);
            writer.WriteStartArray();
            foreach (string item in ProvidersUsed)
            {
                if (item == null)
                {
                    writer.WriteNullValue();
                    continue;
                }
                writer.WriteStringValue(item);
            }
            writer.WriteEndArray();
            if (Optional.IsDefined(EstimatedCostUsd))
            {
                writer.WritePropertyName("estimated_cost_usd"u8);
                writer.WriteNumberValue(EstimatedCostUsd.Value);
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

        SessionGenAiUsage IJsonModel<SessionGenAiUsage>.Create(ref Utf8JsonReader reader, ModelReaderWriterOptions options) => JsonModelCreateCore(ref reader, options);

        protected virtual SessionGenAiUsage JsonModelCreateCore(ref Utf8JsonReader reader, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<SessionGenAiUsage>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(SessionGenAiUsage)} does not support reading '{format}' format.");
            }
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            return DeserializeSessionGenAiUsage(document.RootElement, options);
        }

        internal static SessionGenAiUsage DeserializeSessionGenAiUsage(JsonElement element, ModelReaderWriterOptions options)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }
            int requestCount = default;
            long totalInputTokens = default;
            long totalOutputTokens = default;
            IList<string> modelsUsed = default;
            IList<string> providersUsed = default;
            double? estimatedCostUsd = default;
            IDictionary<string, BinaryData> additionalBinaryDataProperties = new ChangeTrackingDictionary<string, BinaryData>();
            foreach (var prop in element.EnumerateObject())
            {
                if (prop.NameEquals("request_count"u8))
                {
                    requestCount = prop.Value.GetInt32();
                    continue;
                }
                if (prop.NameEquals("total_input_tokens"u8))
                {
                    totalInputTokens = prop.Value.GetInt64();
                    continue;
                }
                if (prop.NameEquals("total_output_tokens"u8))
                {
                    totalOutputTokens = prop.Value.GetInt64();
                    continue;
                }
                if (prop.NameEquals("models_used"u8))
                {
                    List<string> array = new List<string>();
                    foreach (var item in prop.Value.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.Null)
                        {
                            array.Add(null);
                        }
                        else
                        {
                            array.Add(item.GetString());
                        }
                    }
                    modelsUsed = array;
                    continue;
                }
                if (prop.NameEquals("providers_used"u8))
                {
                    List<string> array = new List<string>();
                    foreach (var item in prop.Value.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.Null)
                        {
                            array.Add(null);
                        }
                        else
                        {
                            array.Add(item.GetString());
                        }
                    }
                    providersUsed = array;
                    continue;
                }
                if (prop.NameEquals("estimated_cost_usd"u8))
                {
                    if (prop.Value.ValueKind == JsonValueKind.Null)
                    {
                        continue;
                    }
                    estimatedCostUsd = prop.Value.GetDouble();
                    continue;
                }
                if (options.Format != "W")
                {
                    additionalBinaryDataProperties.Add(prop.Name, BinaryData.FromString(prop.Value.GetRawText()));
                }
            }
            return new SessionGenAiUsage(
                requestCount,
                totalInputTokens,
                totalOutputTokens,
                modelsUsed,
                providersUsed,
                estimatedCostUsd,
                additionalBinaryDataProperties);
        }
    }
}
