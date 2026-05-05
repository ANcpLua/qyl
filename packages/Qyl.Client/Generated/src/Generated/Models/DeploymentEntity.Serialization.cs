
#nullable disable

using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Text.Json;
using Qyl.Client;

namespace Qyl.Domains.Ops.Deployment
{
    public partial class DeploymentEntity : IJsonModel<DeploymentEntity>
    {
        internal DeploymentEntity()
        {
        }

        protected virtual DeploymentEntity PersistableModelCreateCore(BinaryData data, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<DeploymentEntity>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    using (JsonDocument document = JsonDocument.Parse(data, ModelSerializationExtensions.JsonDocumentOptions))
                    {
                        return DeserializeDeploymentEntity(document.RootElement, options);
                    }
                default:
                    throw new FormatException($"The model {nameof(DeploymentEntity)} does not support reading '{options.Format}' format.");
            }
        }

        protected virtual BinaryData PersistableModelWriteCore(ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<DeploymentEntity>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    return ModelReaderWriter.Write(this, options, QylClientContext.Default);
                default:
                    throw new FormatException($"The model {nameof(DeploymentEntity)} does not support writing '{options.Format}' format.");
            }
        }

        BinaryData IPersistableModel<DeploymentEntity>.Write(ModelReaderWriterOptions options) => PersistableModelWriteCore(options);

        DeploymentEntity IPersistableModel<DeploymentEntity>.Create(BinaryData data, ModelReaderWriterOptions options) => PersistableModelCreateCore(data, options);

        string IPersistableModel<DeploymentEntity>.GetFormatFromOptions(ModelReaderWriterOptions options) => "J";

        public static explicit operator DeploymentEntity(ClientResult result)
        {
            PipelineResponse response = result.GetRawResponse();
            using JsonDocument document = JsonDocument.Parse(response.Content, ModelSerializationExtensions.JsonDocumentOptions);
            return DeserializeDeploymentEntity(document.RootElement, ModelSerializationExtensions.WireOptions);
        }

        void IJsonModel<DeploymentEntity>.Write(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            writer.WriteStartObject();
            JsonModelWriteCore(writer, options);
            writer.WriteEndObject();
        }

        protected virtual void JsonModelWriteCore(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<DeploymentEntity>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(DeploymentEntity)} does not support writing '{format}' format.");
            }
            writer.WritePropertyName("deployment.id"u8);
            writer.WriteStringValue(DeploymentId);
            writer.WritePropertyName("service.name"u8);
            writer.WriteStringValue(ServiceName);
            writer.WritePropertyName("service.version"u8);
            writer.WriteStringValue(ServiceVersion);
            writer.WritePropertyName("environment"u8);
            writer.WriteStringValue(Environment.ToSerialString());
            writer.WritePropertyName("status"u8);
            writer.WriteStringValue(Status.ToSerialString());
            writer.WritePropertyName("strategy"u8);
            writer.WriteStringValue(Strategy.ToSerialString());
            writer.WritePropertyName("start_time"u8);
            writer.WriteStringValue(StartTime, "O");
            if (Optional.IsDefined(EndTime))
            {
                writer.WritePropertyName("end_time"u8);
                writer.WriteStringValue(EndTime.Value, "O");
            }
            if (Optional.IsDefined(DurationS))
            {
                writer.WritePropertyName("duration_s"u8);
                writer.WriteNumberValue(DurationS.Value);
            }
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
            if (Optional.IsDefined(PreviousVersion))
            {
                writer.WritePropertyName("previous_version"u8);
                writer.WriteStringValue(PreviousVersion);
            }
            if (Optional.IsDefined(RollbackTarget))
            {
                writer.WritePropertyName("rollback_target"u8);
                writer.WriteStringValue(RollbackTarget);
            }
            if (Optional.IsDefined(ReplicaCount))
            {
                writer.WritePropertyName("replica_count"u8);
                writer.WriteNumberValue(ReplicaCount.Value);
            }
            if (Optional.IsDefined(HealthyReplicas))
            {
                writer.WritePropertyName("healthy_replicas"u8);
                writer.WriteNumberValue(HealthyReplicas.Value);
            }
            if (Optional.IsDefined(ErrorMessage))
            {
                writer.WritePropertyName("error_message"u8);
                writer.WriteStringValue(ErrorMessage);
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

        DeploymentEntity IJsonModel<DeploymentEntity>.Create(ref Utf8JsonReader reader, ModelReaderWriterOptions options) => JsonModelCreateCore(ref reader, options);

        protected virtual DeploymentEntity JsonModelCreateCore(ref Utf8JsonReader reader, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<DeploymentEntity>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(DeploymentEntity)} does not support reading '{format}' format.");
            }
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            return DeserializeDeploymentEntity(document.RootElement, options);
        }

        internal static DeploymentEntity DeserializeDeploymentEntity(JsonElement element, ModelReaderWriterOptions options)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }
            string deploymentId = default;
            string serviceName = default;
            string serviceVersion = default;
            DeploymentEnvironment environment = default;
            DeploymentStatus status = default;
            DeploymentStrategy strategy = default;
            DateTimeOffset startTime = default;
            DateTimeOffset? endTime = default;
            double? durationS = default;
            string deployedBy = default;
            string gitCommit = default;
            string gitBranch = default;
            string previousVersion = default;
            string rollbackTarget = default;
            int? replicaCount = default;
            int? healthyReplicas = default;
            string errorMessage = default;
            IDictionary<string, BinaryData> additionalBinaryDataProperties = new ChangeTrackingDictionary<string, BinaryData>();
            foreach (var prop in element.EnumerateObject())
            {
                if (prop.NameEquals("deployment.id"u8))
                {
                    deploymentId = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("service.name"u8))
                {
                    serviceName = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("service.version"u8))
                {
                    serviceVersion = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("environment"u8))
                {
                    environment = prop.Value.GetString().ToDeploymentEnvironment();
                    continue;
                }
                if (prop.NameEquals("status"u8))
                {
                    status = prop.Value.GetString().ToDeploymentStatus();
                    continue;
                }
                if (prop.NameEquals("strategy"u8))
                {
                    strategy = prop.Value.GetString().ToDeploymentStrategy();
                    continue;
                }
                if (prop.NameEquals("start_time"u8))
                {
                    startTime = prop.Value.GetDateTimeOffset("O");
                    continue;
                }
                if (prop.NameEquals("end_time"u8))
                {
                    if (prop.Value.ValueKind == JsonValueKind.Null)
                    {
                        continue;
                    }
                    endTime = prop.Value.GetDateTimeOffset("O");
                    continue;
                }
                if (prop.NameEquals("duration_s"u8))
                {
                    if (prop.Value.ValueKind == JsonValueKind.Null)
                    {
                        continue;
                    }
                    durationS = prop.Value.GetDouble();
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
                if (prop.NameEquals("previous_version"u8))
                {
                    previousVersion = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("rollback_target"u8))
                {
                    rollbackTarget = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("replica_count"u8))
                {
                    if (prop.Value.ValueKind == JsonValueKind.Null)
                    {
                        continue;
                    }
                    replicaCount = prop.Value.GetInt32();
                    continue;
                }
                if (prop.NameEquals("healthy_replicas"u8))
                {
                    if (prop.Value.ValueKind == JsonValueKind.Null)
                    {
                        continue;
                    }
                    healthyReplicas = prop.Value.GetInt32();
                    continue;
                }
                if (prop.NameEquals("error_message"u8))
                {
                    errorMessage = prop.Value.GetString();
                    continue;
                }
                if (options.Format != "W")
                {
                    additionalBinaryDataProperties.Add(prop.Name, BinaryData.FromString(prop.Value.GetRawText()));
                }
            }
            return new DeploymentEntity(
                deploymentId,
                serviceName,
                serviceVersion,
                environment,
                status,
                strategy,
                startTime,
                endTime,
                durationS,
                deployedBy,
                gitCommit,
                gitBranch,
                previousVersion,
                rollbackTarget,
                replicaCount,
                healthyReplicas,
                errorMessage,
                additionalBinaryDataProperties);
        }
    }
}
