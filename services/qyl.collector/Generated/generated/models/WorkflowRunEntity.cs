
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;

namespace Qyl.Domains.Workflow
{

    public partial class WorkflowRunEntity
    {
        public string Id { get; set; }

        [JsonPropertyName("workflow_id")]
        public string WorkflowId { get; set; }

        [JsonPropertyName("workflow_version")]
        public int WorkflowVersion { get; set; }

        [JsonPropertyName("project_id")]
        public string ProjectId { get; set; }

        [JsonPropertyName("trigger_type")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public WorkflowTriggerType TriggerType { get; set; }

        [JsonPropertyName("trigger_source")]
        public string TriggerSource { get; set; }

        [JsonPropertyName("input_json")]
        public string InputJson { get; set; }

        [JsonPropertyName("output_json")]
        public string OutputJson { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public WorkflowRunStatus Status { get; set; }

        [JsonPropertyName("error_message")]
        public string ErrorMessage { get; set; }

        [JsonPropertyName("parent_run_id")]
        public string ParentRunId { get; set; }

        [JsonPropertyName("correlation_id")]
        public string CorrelationId { get; set; }

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
