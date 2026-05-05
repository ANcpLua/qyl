
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;

namespace Qyl.Domains.Workflow
{

    public partial class WorkflowNodeEntity
    {
        public string Id { get; set; }

        [JsonPropertyName("run_id")]
        public string RunId { get; set; }

        [JsonPropertyName("node_id")]
        public string NodeId { get; set; }

        [JsonPropertyName("node_type")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public WorkflowNodeType NodeType { get; set; }

        [JsonPropertyName("node_name")]
        public string NodeName { get; set; }

        public int Attempt { get; set; }

        [JsonPropertyName("input_json")]
        public string InputJson { get; set; }

        [JsonPropertyName("output_json")]
        public string OutputJson { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public WorkflowRunStatus Status { get; set; }

        [JsonPropertyName("error_message")]
        public string ErrorMessage { get; set; }

        [JsonPropertyName("retry_count")]
        public int RetryCount { get; set; }

        [JsonPropertyName("max_retries")]
        public int MaxRetries { get; set; }

        [JsonPropertyName("timeout_ms")]
        public int? TimeoutMs { get; set; }

        [JsonPropertyName("started_at")]
        public DateTimeOffset? StartedAt { get; set; }

        [JsonPropertyName("completed_at")]
        public DateTimeOffset? CompletedAt { get; set; }

        [JsonPropertyName("duration_ms")]
        public int? DurationMs { get; set; }

        [JsonPropertyName("created_at")]
        public DateTimeOffset CreatedAt { get; set; }


    }
}
