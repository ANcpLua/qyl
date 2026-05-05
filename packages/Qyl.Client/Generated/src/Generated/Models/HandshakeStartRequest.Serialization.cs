
#nullable disable

using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Text.Json;
using Qyl.Client;

namespace Qyl.Api
{
    public partial class HandshakeStartRequest : IJsonModel<HandshakeStartRequest>
    {
        internal HandshakeStartRequest()
        {
        }

        protected virtual HandshakeStartRequest PersistableModelCreateCore(BinaryData data, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<HandshakeStartRequest>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    using (JsonDocument document = JsonDocument.Parse(data, ModelSerializationExtensions.JsonDocumentOptions))
                    {
                        return DeserializeHandshakeStartRequest(document.RootElement, options);
                    }
                default:
                    throw new FormatException($"The model {nameof(HandshakeStartRequest)} does not support reading '{options.Format}' format.");
            }
        }

        protected virtual BinaryData PersistableModelWriteCore(ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<HandshakeStartRequest>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    return ModelReaderWriter.Write(this, options, QylClientContext.Default);
                default:
                    throw new FormatException($"The model {nameof(HandshakeStartRequest)} does not support writing '{options.Format}' format.");
            }
        }

        BinaryData IPersistableModel<HandshakeStartRequest>.Write(ModelReaderWriterOptions options) => PersistableModelWriteCore(options);

        HandshakeStartRequest IPersistableModel<HandshakeStartRequest>.Create(BinaryData data, ModelReaderWriterOptions options) => PersistableModelCreateCore(data, options);

        string IPersistableModel<HandshakeStartRequest>.GetFormatFromOptions(ModelReaderWriterOptions options) => "J";

        public static implicit operator BinaryContent(HandshakeStartRequest handshakeStartRequest)
        {
            if (handshakeStartRequest == null)
            {
                return null;
            }
            return BinaryContent.Create(handshakeStartRequest, ModelSerializationExtensions.WireOptions);
        }

        void IJsonModel<HandshakeStartRequest>.Write(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            writer.WriteStartObject();
            JsonModelWriteCore(writer, options);
            writer.WriteEndObject();
        }

        protected virtual void JsonModelWriteCore(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<HandshakeStartRequest>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(HandshakeStartRequest)} does not support writing '{format}' format.");
            }
            writer.WritePropertyName("code_challenge"u8);
            writer.WriteStringValue(CodeChallenge);
            writer.WritePropertyName("client_id"u8);
            writer.WriteStringValue(ClientId);
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

        HandshakeStartRequest IJsonModel<HandshakeStartRequest>.Create(ref Utf8JsonReader reader, ModelReaderWriterOptions options) => JsonModelCreateCore(ref reader, options);

        protected virtual HandshakeStartRequest JsonModelCreateCore(ref Utf8JsonReader reader, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<HandshakeStartRequest>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(HandshakeStartRequest)} does not support reading '{format}' format.");
            }
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            return DeserializeHandshakeStartRequest(document.RootElement, options);
        }

        internal static HandshakeStartRequest DeserializeHandshakeStartRequest(JsonElement element, ModelReaderWriterOptions options)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }
            string codeChallenge = default;
            string clientId = default;
            IDictionary<string, BinaryData> additionalBinaryDataProperties = new ChangeTrackingDictionary<string, BinaryData>();
            foreach (var prop in element.EnumerateObject())
            {
                if (prop.NameEquals("code_challenge"u8))
                {
                    codeChallenge = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("client_id"u8))
                {
                    clientId = prop.Value.GetString();
                    continue;
                }
                if (options.Format != "W")
                {
                    additionalBinaryDataProperties.Add(prop.Name, BinaryData.FromString(prop.Value.GetRawText()));
                }
            }
            return new HandshakeStartRequest(codeChallenge, clientId, additionalBinaryDataProperties);
        }
    }
}
