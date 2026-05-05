
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;

namespace Qyl.Domains.Issues
{

    public partial class ErrorIssueEventEntity
    {
        public string Id { get; set; }

        [JsonPropertyName("issue_id")]
        public string IssueId { get; set; }

        [JsonPropertyName("trace_id")]
        public string TraceId { get; set; }

        [JsonPropertyName("span_id")]
        public string SpanId { get; set; }

        public string Message { get; set; }

        [JsonPropertyName("stack_trace")]
        public string StackTrace { get; set; }

        [JsonPropertyName("stack_frames_json")]
        public string StackFramesJson { get; set; }

        public string Environment { get; set; }

        [JsonPropertyName("release_version")]
        public string ReleaseVersion { get; set; }

        [JsonPropertyName("user_id")]
        public string UserId { get; set; }

        [JsonPropertyName("user_ip")]
        public string UserIp { get; set; }

        [JsonPropertyName("request_url")]
        public string RequestUrl { get; set; }

        [JsonPropertyName("request_method")]
        public string RequestMethod { get; set; }

        public string Browser { get; set; }

        public string Os { get; set; }

        public string Device { get; set; }

        public string Runtime { get; set; }

        [JsonPropertyName("runtime_version")]
        public string RuntimeVersion { get; set; }

        [JsonPropertyName("context_json")]
        public string ContextJson { get; set; }

        [JsonPropertyName("tags_json")]
        public string TagsJson { get; set; }

        public DateTimeOffset Timestamp { get; set; }


    }
}
