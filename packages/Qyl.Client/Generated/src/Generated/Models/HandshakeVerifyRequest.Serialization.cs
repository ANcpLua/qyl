
#nullable disable

using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Text.Json;
using Qyl.Client;

namespace Qyl.Api
{
    public partial class HandshakeVerifyRequest : IJsonModel<HandshakeVerifyRequest>
    {
        internal HandshakeVerifyRequest()
        {
        }

        protected virtual HandshakeVerifyRequest PersistableModelCreateCore(BinaryData data, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<HandshakeVerifyRequest>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    using (JsonDocument document = JsonDocument.Parse(data, ModelSerializationExtensions.JsonDocumentOptions))
                    {
                        return DeserializeHandshakeVerifyRequest(document.RootElement, options);
                    }
                default:
                    throw new FormatException($"The model {nameof(HandshakeVerifyRequest)} does not support reading '{options.Format}' format.");
            }
        }

        protected virtual BinaryData PersistableModelWriteCore(ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<HandshakeVerifyRequest>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    return ModelReaderWriter.Write(this, options, QylClientContext.Default);
                default:
                    throw new FormatException($"The model {nameof(HandshakeVerifyRequest)} does not support writing '{options.Format}' format.");
            }
        }

        BinaryData IPersistableModel<HandshakeVerifyRequest>.Write(ModelReaderWriterOptions options) => PersistableModelWriteCore(options);

        HandshakeVerifyRequest IPersistableModel<HandshakeVerifyRequest>.Create(BinaryData data, ModelReaderWriterOptions options) => PersistableModelCreateCore(data, options);

        string IPersistableModel<HandshakeVerifyRequest>.GetFormatFromOptions(ModelReaderWriterOptions options) => "J";

        public static implicit operator BinaryContent(HandshakeVerifyRequest handshakeVerifyRequest)
        {
            if (handshakeVerifyRequest == null)
            {
                return null;
            }
            return BinaryContent.Create(handshakeVerifyRequest, ModelSerializationExtensions.WireOptions);
        }

        void IJsonModel<HandshakeVerifyRequest>.Write(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            writer.WriteStartObject();
            JsonModelWriteCore(writer, options);
            writer.WriteEndObject();
        }

        protected virtual void JsonModelWriteCore(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<HandshakeVerifyRequest>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(HandshakeVerifyRequest)} does not support writing '{format}' format.");
            }
            writer.WritePropertyName("code_verifier"u8);
            writer.WriteStringValue(CodeVerifier);
            writer.WritePropertyName("code"u8);
            writer.WriteStringValue(Code);
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

        HandshakeVerifyRequest IJsonModel<HandshakeVerifyRequest>.Create(ref Utf8JsonReader reader, ModelReaderWriterOptions options) => JsonModelCreateCore(ref reader, options);

        protected virtual HandshakeVerifyRequest JsonModelCreateCore(ref Utf8JsonReader reader, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<HandshakeVerifyRequest>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(HandshakeVerifyRequest)} does not support reading '{format}' format.");
            }
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            return DeserializeHandshakeVerifyRequest(document.RootElement, options);
        }

        internal static HandshakeVerifyRequest DeserializeHandshakeVerifyRequest(JsonElement element, ModelReaderWriterOptions options)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }
            string codeVerifier = default;
            string code = default;
            IDictionary<string, BinaryData> additionalBinaryDataProperties = new ChangeTrackingDictionary<string, BinaryData>();
            foreach (var prop in element.EnumerateObject())
            {
                if (prop.NameEquals("code_verifier"u8))
                {
                    codeVerifier = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("code"u8))
                {
                    code = prop.Value.GetString();
                    continue;
                }
                if (options.Format != "W")
                {
                    additionalBinaryDataProperties.Add(prop.Name, BinaryData.FromString(prop.Value.GetRawText()));
                }
            }
            return new HandshakeVerifyRequest(codeVerifier, code, additionalBinaryDataProperties);
        }
    }
}
