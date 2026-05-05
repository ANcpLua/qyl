
#nullable disable

using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Text.Json;
using Qyl.Client;
using Qyl.Domains.Ops.Deployment;

namespace Qyl.Api
{
    public partial class DeploymentCreate : IJsonModel<DeploymentCreate>
    {
        internal DeploymentCreate()
        {
        }

        protected virtual DeploymentCreate PersistableModelCreateCore(BinaryData data, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<DeploymentCreate>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    using (JsonDocument document = JsonDocument.Parse(data, ModelSerializationExtensions.JsonDocumentOptions))
                    {
                        return DeserializeDeploymentCreate(document.RootElement, options);
                    }
                default:
                    throw new FormatException($"The model {nameof(DeploymentCreate)} does not support reading '{options.Format}' format.");
            }
        }

        protected virtual BinaryData PersistableModelWriteCore(ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<DeploymentCreate>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    return ModelReaderWriter.Write(this, options, QylClientContext.Default);
                default:
                    throw new FormatException($"The model {nameof(DeploymentCreate)} does not support writing '{options.Format}' format.");
            }
        }

        BinaryData IPersistableModel<DeploymentCreate>.Write(ModelReaderWriterOptions options) => PersistableModelWriteCore(options);

        DeploymentCreate IPersistableModel<DeploymentCreate>.Create(BinaryData data, ModelReaderWriterOptions options) => PersistableModelCreateCore(data, options);

        string IPersistableModel<DeploymentCreate>.GetFormatFromOptions(ModelReaderWriterOptions options) => "J";

        public static implicit operator BinaryContent(DeploymentCreate deploymentCreate)
        {
            if (deploymentCreate == null)
            {
                return null;
            }
            return BinaryContent.Create(deploymentCreate, ModelSerializationExtensions.WireOptions);
        }

        void IJsonModel<DeploymentCreate>.Write(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            writer.WriteStartObject();
            JsonModelWriteCore(writer, options);
            writer.WriteEndObject();
        }

        protected virtual void JsonModelWriteCore(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<DeploymentCreate>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(DeploymentCreate)} does not support writing '{format}' format.");
            }
            writer.WritePropertyName("service_name"u8);
            writer.WriteStringValue(ServiceName);
            writer.WritePropertyName("service_version"u8);
            writer.WriteStringValue(ServiceVersion);
            writer.WritePropertyName("environment"u8);
            writer.WriteStringValue(Environment.ToSerialString());
            writer.WritePropertyName("strategy"u8);
            writer.WriteStringValue(Strategy.ToSerialString());
            if (Optional.IsDefined(DeployedBy))
            {
                writer.WritePropertyName("deployed_by"u8);
                writer.WriteStringValue(DeployedBy);
            }
            if (Optional.IsDefined(GitCommit))
            {
                writer.WritePropertyName("git_commit"u8);
                writer.WriteStringValue(GitCommit);
            }
            if (Optional.IsDefined(GitBranch))
            {
                writer.WritePropertyName("git_branch"u8);
                writer.WriteStringValue(GitBranch);
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

        DeploymentCreate IJsonModel<DeploymentCreate>.Create(ref Utf8JsonReader reader, ModelReaderWriterOptions options) => JsonModelCreateCore(ref reader, options);

        protected virtual DeploymentCreate JsonModelCreateCore(ref Utf8JsonReader reader, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<DeploymentCreate>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(DeploymentCreate)} does not support reading '{format}' format.");
            }
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            return DeserializeDeploymentCreate(document.RootElement, options);
        }

        internal static DeploymentCreate DeserializeDeploymentCreate(JsonElement element, ModelReaderWriterOptions options)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }
            string serviceName = default;
            string serviceVersion = default;
            DeploymentEnvironment environment = default;
            DeploymentStrategy strategy = default;
            string deployedBy = default;
            string gitCommit = default;
            string gitBranch = default;
            IDictionary<string, BinaryData> additionalBinaryDataProperties = new ChangeTrackingDictionary<string, BinaryData>();
            foreach (var prop in element.EnumerateObject())
            {
                if (prop.NameEquals("service_name"u8))
                {
                    serviceName = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("service_version"u8))
                {
                    serviceVersion = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("environment"u8))
                {
                    environment = prop.Value.GetString().ToDeploymentEnvironment();
                    continue;
                }
                if (prop.NameEquals("strategy"u8))
                {
                    strategy = prop.Value.GetString().ToDeploymentStrategy();
                    continue;
                }
                if (prop.NameEquals("deployed_by"u8))
                {
                    deployedBy = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("git_commit"u8))
                {
                    gitCommit = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("git_branch"u8))
                {
                    gitBranch = prop.Value.GetString();
                    continue;
                }
                if (options.Format != "W")
                {
                    additionalBinaryDataProperties.Add(prop.Name, BinaryData.FromString(prop.Value.GetRawText()));
                }
            }
            return new DeploymentCreate(
                serviceName,
                serviceVersion,
                environment,
                strategy,
                deployedBy,
                gitCommit,
                gitBranch,
                additionalBinaryDataProperties);
        }
    }
}
