
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;

namespace Qyl.Domains.Configurator
{

    public partial class GenerationJobEntity
    {
        public string Id { get; set; }

        [JsonPropertyName("workspace_id")]
        public string WorkspaceId { get; set; }

        [JsonPropertyName("profile_id")]
        public string ProfileId { get; set; }

        [JsonPropertyName("job_type")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public GenerationJobType JobType { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public JobStatus Status { get; set; }

        public int Priority { get; set; }

        [JsonPropertyName("input_hash")]
        public string InputHash { get; set; }

        [JsonPropertyName("output_path")]
        public string OutputPath { get; set; }

        [JsonPropertyName("output_hash")]
        public string OutputHash { get; set; }

        [JsonPropertyName("error_message")]
        public string ErrorMessage { get; set; }

        [JsonPropertyName("queued_at")]
        public DateTimeOffset QueuedAt { get; set; }

        [JsonPropertyName("started_at")]
        public DateTimeOffset? StartedAt { get; set; }

        [JsonPropertyName("completed_at")]
        public DateTimeOffset? CompletedAt { get; set; }

        [JsonPropertyName("duration_ms")]
        public int? DurationMs { get; set; }


    }
}
