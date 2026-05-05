
#nullable disable

using System;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Text.Json;
using Qyl.Client;

namespace Qyl.Domains.Issues
{
    public partial class ErrorIssueEventEntity : IJsonModel<ErrorIssueEventEntity>
    {
        internal ErrorIssueEventEntity()
        {
        }

        protected virtual ErrorIssueEventEntity PersistableModelCreateCore(BinaryData data, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<ErrorIssueEventEntity>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    using (JsonDocument document = JsonDocument.Parse(data, ModelSerializationExtensions.JsonDocumentOptions))
                    {
                        return DeserializeErrorIssueEventEntity(document.RootElement, options);
                    }
                default:
                    throw new FormatException($"The model {nameof(ErrorIssueEventEntity)} does not support reading '{options.Format}' format.");
            }
        }

        protected virtual BinaryData PersistableModelWriteCore(ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<ErrorIssueEventEntity>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    return ModelReaderWriter.Write(this, options, QylClientContext.Default);
                default:
                    throw new FormatException($"The model {nameof(ErrorIssueEventEntity)} does not support writing '{options.Format}' format.");
            }
        }

        BinaryData IPersistableModel<ErrorIssueEventEntity>.Write(ModelReaderWriterOptions options) => PersistableModelWriteCore(options);

        ErrorIssueEventEntity IPersistableModel<ErrorIssueEventEntity>.Create(BinaryData data, ModelReaderWriterOptions options) => PersistableModelCreateCore(data, options);

        string IPersistableModel<ErrorIssueEventEntity>.GetFormatFromOptions(ModelReaderWriterOptions options) => "J";

        void IJsonModel<ErrorIssueEventEntity>.Write(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            writer.WriteStartObject();
            JsonModelWriteCore(writer, options);
            writer.WriteEndObject();
        }

        protected virtual void JsonModelWriteCore(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<ErrorIssueEventEntity>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(ErrorIssueEventEntity)} does not support writing '{format}' format.");
            }
            writer.WritePropertyName("id"u8);
            writer.WriteStringValue(Id);
            writer.WritePropertyName("issue_id"u8);
            writer.WriteStringValue(IssueId);
            if (Optional.IsDefined(TraceId))
            {
                writer.WritePropertyName("trace_id"u8);
                writer.WriteStringValue(TraceId);
            }
            if (Optional.IsDefined(SpanId))
            {
                writer.WritePropertyName("span_id"u8);
                writer.WriteStringValue(SpanId);
            }
            if (Optional.IsDefined(Message))
            {
                writer.WritePropertyName("message"u8);
                writer.WriteStringValue(Message);
            }
            if (Optional.IsDefined(StackTrace))
            {
                writer.WritePropertyName("stack_trace"u8);
                writer.WriteStringValue(StackTrace);
            }
            if (Optional.IsDefined(StackFramesJson))
            {
                writer.WritePropertyName("stack_frames_json"u8);
                writer.WriteStringValue(StackFramesJson);
            }
            if (Optional.IsDefined(Environment))
            {
                writer.WritePropertyName("environment"u8);
                writer.WriteStringValue(Environment);
            }
            if (Optional.IsDefined(ReleaseVersion))
            {
                writer.WritePropertyName("release_version"u8);
                writer.WriteStringValue(ReleaseVersion);
            }
            if (Optional.IsDefined(UserId))
            {
                writer.WritePropertyName("user_id"u8);
                writer.WriteStringValue(UserId);
            }
            if (Optional.IsDefined(UserIp))
            {
                writer.WritePropertyName("user_ip"u8);
                writer.WriteStringValue(UserIp);
            }
            if (Optional.IsDefined(RequestUrl))
            {
                writer.WritePropertyName("request_url"u8);
                writer.WriteStringValue(RequestUrl);
            }
            if (Optional.IsDefined(RequestMethod))
            {
                writer.WritePropertyName("request_method"u8);
                writer.WriteStringValue(RequestMethod);
            }
            if (Optional.IsDefined(Browser))
            {
                writer.WritePropertyName("browser"u8);
                writer.WriteStringValue(Browser);
            }
            if (Optional.IsDefined(Os))
            {
                writer.WritePropertyName("os"u8);
                writer.WriteStringValue(Os);
            }
            if (Optional.IsDefined(Device))
            {
                writer.WritePropertyName("device"u8);
                writer.WriteStringValue(Device);
            }
            if (Optional.IsDefined(Runtime))
            {
                writer.WritePropertyName("runtime"u8);
                writer.WriteStringValue(Runtime);
            }
            if (Optional.IsDefined(RuntimeVersion))
            {
                writer.WritePropertyName("runtime_version"u8);
                writer.WriteStringValue(RuntimeVersion);
            }
            if (Optional.IsDefined(ContextJson))
            {
                writer.WritePropertyName("context_json"u8);
                writer.WriteStringValue(ContextJson);
            }
            if (Optional.IsDefined(TagsJson))
            {
                writer.WritePropertyName("tags_json"u8);
                writer.WriteStringValue(TagsJson);
            }
            writer.WritePropertyName("timestamp"u8);
            writer.WriteStringValue(Timestamp, "O");
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

        ErrorIssueEventEntity IJsonModel<ErrorIssueEventEntity>.Create(ref Utf8JsonReader reader, ModelReaderWriterOptions options) => JsonModelCreateCore(ref reader, options);

        protected virtual ErrorIssueEventEntity JsonModelCreateCore(ref Utf8JsonReader reader, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<ErrorIssueEventEntity>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(ErrorIssueEventEntity)} does not support reading '{format}' format.");
            }
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            return DeserializeErrorIssueEventEntity(document.RootElement, options);
        }

        internal static ErrorIssueEventEntity DeserializeErrorIssueEventEntity(JsonElement element, ModelReaderWriterOptions options)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }
            string id = default;
            string issueId = default;
            string traceId = default;
            string spanId = default;
            string message = default;
            string stackTrace = default;
            string stackFramesJson = default;
            string environment = default;
            string releaseVersion = default;
            string userId = default;
            string userIp = default;
            string requestUrl = default;
            string requestMethod = default;
            string browser = default;
            string os = default;
            string device = default;
            string runtime = default;
            string runtimeVersion = default;
            string contextJson = default;
            string tagsJson = default;
            DateTimeOffset timestamp = default;
            IDictionary<string, BinaryData> additionalBinaryDataProperties = new ChangeTrackingDictionary<string, BinaryData>();
            foreach (var prop in element.EnumerateObject())
            {
                if (prop.NameEquals("id"u8))
                {
                    id = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("issue_id"u8))
                {
                    issueId = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("trace_id"u8))
                {
                    traceId = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("span_id"u8))
                {
                    spanId = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("message"u8))
                {
                    message = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("stack_trace"u8))
                {
                    stackTrace = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("stack_frames_json"u8))
                {
                    stackFramesJson = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("environment"u8))
                {
                    environment = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("release_version"u8))
                {
                    releaseVersion = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("user_id"u8))
                {
                    userId = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("user_ip"u8))
                {
                    userIp = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("request_url"u8))
                {
                    requestUrl = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("request_method"u8))
                {
                    requestMethod = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("browser"u8))
                {
                    browser = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("os"u8))
                {
                    os = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("device"u8))
                {
                    device = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("runtime"u8))
                {
                    runtime = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("runtime_version"u8))
                {
                    runtimeVersion = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("context_json"u8))
                {
                    contextJson = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("tags_json"u8))
                {
                    tagsJson = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("timestamp"u8))
                {
                    timestamp = prop.Value.GetDateTimeOffset("O");
                    continue;
                }
                if (options.Format != "W")
                {
                    additionalBinaryDataProperties.Add(prop.Name, BinaryData.FromString(prop.Value.GetRawText()));
                }
            }
            return new ErrorIssueEventEntity(
                id,
                issueId,
                traceId,
                spanId,
                message,
                stackTrace,
                stackFramesJson,
                environment,
                releaseVersion,
                userId,
                userIp,
                requestUrl,
                requestMethod,
                browser,
                os,
                device,
                runtime,
                runtimeVersion,
                contextJson,
                tagsJson,
                timestamp,
                additionalBinaryDataProperties);
        }
    }
}
