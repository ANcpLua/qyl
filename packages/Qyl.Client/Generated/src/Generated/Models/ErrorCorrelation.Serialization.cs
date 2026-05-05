
#nullable disable

using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Text.Json;
using Qyl.Client;
using Qyl.Common;

namespace Qyl.Domains.Observe.Error
{
    public partial class ErrorCorrelation : IJsonModel<ErrorCorrelation>
    {
        internal ErrorCorrelation()
        {
        }

        protected virtual ErrorCorrelation PersistableModelCreateCore(BinaryData data, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<ErrorCorrelation>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    using (JsonDocument document = JsonDocument.Parse(data, ModelSerializationExtensions.JsonDocumentOptions))
                    {
                        return DeserializeErrorCorrelation(document.RootElement, options);
                    }
                default:
                    throw new FormatException($"The model {nameof(ErrorCorrelation)} does not support reading '{options.Format}' format.");
            }
        }

        protected virtual BinaryData PersistableModelWriteCore(ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<ErrorCorrelation>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    return ModelReaderWriter.Write(this, options, QylClientContext.Default);
                default:
                    throw new FormatException($"The model {nameof(ErrorCorrelation)} does not support writing '{options.Format}' format.");
            }
        }

        BinaryData IPersistableModel<ErrorCorrelation>.Write(ModelReaderWriterOptions options) => PersistableModelWriteCore(options);

        ErrorCorrelation IPersistableModel<ErrorCorrelation>.Create(BinaryData data, ModelReaderWriterOptions options) => PersistableModelCreateCore(data, options);

        string IPersistableModel<ErrorCorrelation>.GetFormatFromOptions(ModelReaderWriterOptions options) => "J";

        public static explicit operator ErrorCorrelation(ClientResult result)
        {
            PipelineResponse response = result.GetRawResponse();
            using JsonDocument document = JsonDocument.Parse(response.Content, ModelSerializationExtensions.JsonDocumentOptions);
            return DeserializeErrorCorrelation(document.RootElement, ModelSerializationExtensions.WireOptions);
        }

        void IJsonModel<ErrorCorrelation>.Write(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            writer.WriteStartObject();
            JsonModelWriteCore(writer, options);
            writer.WriteEndObject();
        }

        protected virtual void JsonModelWriteCore(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<ErrorCorrelation>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(ErrorCorrelation)} does not support writing '{format}' format.");
            }
            writer.WritePropertyName("error_id"u8);
            writer.WriteStringValue(ErrorId);
            writer.WritePropertyName("correlated_errors"u8);
            writer.WriteStartArray();
            foreach (CorrelatedError item in CorrelatedErrors)
            {
                writer.WriteObjectValue(item, options);
            }
            writer.WriteEndArray();
            if (Optional.IsDefined(RootCause))
            {
                writer.WritePropertyName("root_cause"u8);
                writer.WriteStringValue(RootCause);
            }
            if (Optional.IsCollectionDefined(CommonAttributes))
            {
                writer.WritePropertyName("common_attributes"u8);
                writer.WriteStartArray();
                foreach (Common.Attribute item in CommonAttributes)
                {
                    writer.WriteObjectValue(item, options);
                }
                writer.WriteEndArray();
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

        ErrorCorrelation IJsonModel<ErrorCorrelation>.Create(ref Utf8JsonReader reader, ModelReaderWriterOptions options) => JsonModelCreateCore(ref reader, options);

        protected virtual ErrorCorrelation JsonModelCreateCore(ref Utf8JsonReader reader, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<ErrorCorrelation>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(ErrorCorrelation)} does not support reading '{format}' format.");
            }
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            return DeserializeErrorCorrelation(document.RootElement, options);
        }

        internal static ErrorCorrelation DeserializeErrorCorrelation(JsonElement element, ModelReaderWriterOptions options)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }
            string errorId = default;
            IList<CorrelatedError> correlatedErrors = default;
            string rootCause = default;
            IList<Common.Attribute> commonAttributes = default;
            IDictionary<string, BinaryData> additionalBinaryDataProperties = new ChangeTrackingDictionary<string, BinaryData>();
            foreach (var prop in element.EnumerateObject())
            {
                if (prop.NameEquals("error_id"u8))
                {
                    errorId = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("correlated_errors"u8))
                {
                    List<CorrelatedError> array = new List<CorrelatedError>();
                    foreach (var item in prop.Value.EnumerateArray())
                    {
                        array.Add(CorrelatedError.DeserializeCorrelatedError(item, options));
                    }
                    correlatedErrors = array;
                    continue;
                }
                if (prop.NameEquals("root_cause"u8))
                {
                    rootCause = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("common_attributes"u8))
                {
                    if (prop.Value.ValueKind == JsonValueKind.Null)
                    {
                        continue;
                    }
                    List<Common.Attribute> array = new List<Common.Attribute>();
                    foreach (var item in prop.Value.EnumerateArray())
                    {
                        array.Add(Common.Attribute.DeserializeAttribute(item, options));
                    }
                    commonAttributes = array;
                    continue;
                }
                if (options.Format != "W")
                {
                    additionalBinaryDataProperties.Add(prop.Name, BinaryData.FromString(prop.Value.GetRawText()));
                }
            }
            return new ErrorCorrelation(errorId, correlatedErrors, rootCause, commonAttributes ?? new ChangeTrackingList<Common.Attribute>(), additionalBinaryDataProperties);
        }
    }
}
