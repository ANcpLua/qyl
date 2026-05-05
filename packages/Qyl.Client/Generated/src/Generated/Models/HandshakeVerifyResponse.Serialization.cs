
#nullable disable

using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Text.Json;
using Qyl.Client;

namespace Qyl.Api
{
    public partial class HandshakeVerifyResponse : IJsonModel<HandshakeVerifyResponse>
    {
        internal HandshakeVerifyResponse()
        {
        }

        protected virtual HandshakeVerifyResponse PersistableModelCreateCore(BinaryData data, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<HandshakeVerifyResponse>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    using (JsonDocument document = JsonDocument.Parse(data, ModelSerializationExtensions.JsonDocumentOptions))
                    {
                        return DeserializeHandshakeVerifyResponse(document.RootElement, options);
                    }
                default:
                    throw new FormatException($"The model {nameof(HandshakeVerifyResponse)} does not support reading '{options.Format}' format.");
            }
        }

        protected virtual BinaryData PersistableModelWriteCore(ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<HandshakeVerifyResponse>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    return ModelReaderWriter.Write(this, options, QylClientContext.Default);
                default:
                    throw new FormatException($"The model {nameof(HandshakeVerifyResponse)} does not support writing '{options.Format}' format.");
            }
        }

        BinaryData IPersistableModel<HandshakeVerifyResponse>.Write(ModelReaderWriterOptions options) => PersistableModelWriteCore(options);

        HandshakeVerifyResponse IPersistableModel<HandshakeVerifyResponse>.Create(BinaryData data, ModelReaderWriterOptions options) => PersistableModelCreateCore(data, options);

        string IPersistableModel<HandshakeVerifyResponse>.GetFormatFromOptions(ModelReaderWriterOptions options) => "J";

        public static explicit operator HandshakeVerifyResponse(ClientResult result)
        {
            PipelineResponse response = result.GetRawResponse();
            using JsonDocument document = JsonDocument.Parse(response.Content, ModelSerializationExtensions.JsonDocumentOptions);
            return DeserializeHandshakeVerifyResponse(document.RootElement, ModelSerializationExtensions.WireOptions);
        }

        void IJsonModel<HandshakeVerifyResponse>.Write(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            writer.WriteStartObject();
            JsonModelWriteCore(writer, options);
            writer.WriteEndObject();
        }

        protected virtual void JsonModelWriteCore(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<HandshakeVerifyResponse>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(HandshakeVerifyResponse)} does not support writing '{format}' format.");
            }
            writer.WritePropertyName("access_token"u8);
            writer.WriteStringValue(AccessToken);
            writer.WritePropertyName("expires_at"u8);
            writer.WriteStringValue(ExpiresAt, "O");
            writer.WritePropertyName("workspace_id"u8);
            writer.WriteStringValue(WorkspaceId);
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

        HandshakeVerifyResponse IJsonModel<HandshakeVerifyResponse>.Create(ref Utf8JsonReader reader, ModelReaderWriterOptions options) => JsonModelCreateCore(ref reader, options);

        protected virtual HandshakeVerifyResponse JsonModelCreateCore(ref Utf8JsonReader reader, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<HandshakeVerifyResponse>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(HandshakeVerifyResponse)} does not support reading '{format}' format.");
            }
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            return DeserializeHandshakeVerifyResponse(document.RootElement, options);
        }

        internal static HandshakeVerifyResponse DeserializeHandshakeVerifyResponse(JsonElement element, ModelReaderWriterOptions options)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }
            string accessToken = default;
            DateTimeOffset expiresAt = default;
            string workspaceId = default;
            IDictionary<string, BinaryData> additionalBinaryDataProperties = new ChangeTrackingDictionary<string, BinaryData>();
            foreach (var prop in element.EnumerateObject())
            {
                if (prop.NameEquals("access_token"u8))
                {
                    accessToken = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("expires_at"u8))
                {
                    expiresAt = prop.Value.GetDateTimeOffset("O");
                    continue;
                }
                if (prop.NameEquals("workspace_id"u8))
                {
                    workspaceId = prop.Value.GetString();
                    continue;
                }
                if (options.Format != "W")
                {
                    additionalBinaryDataProperties.Add(prop.Name, BinaryData.FromString(prop.Value.GetRawText()));
                }
            }
            return new HandshakeVerifyResponse(accessToken, expiresAt, workspaceId, additionalBinaryDataProperties);
        }
    }
}
