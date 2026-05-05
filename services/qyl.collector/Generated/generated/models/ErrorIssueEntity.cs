
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;

namespace Qyl.Domains.Issues
{

    public partial class ErrorIssueEntity
    {
        public string Id { get; set; }

        [JsonPropertyName("project_id")]
        public string ProjectId { get; set; }

        public string Fingerprint { get; set; }

        public string Title { get; set; }

        public string Culprit { get; set; }

        [JsonPropertyName("error_type")]
        public string ErrorType { get; set; }

        public string Category { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public IssueLevel Level { get; set; }

        public string Platform { get; set; }

        [JsonPropertyName("first_seen_at")]
        public DateTimeOffset FirstSeenAt { get; set; }

        [JsonPropertyName("last_seen_at")]
        public DateTimeOffset LastSeenAt { get; set; }

        [JsonPropertyName("occurrence_count")]
        public long OccurrenceCount { get; set; }

        [JsonPropertyName("affected_users_count")]
        public int AffectedUsersCount { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public IssueStatus Status { get; set; }

        public string Substatus { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public IssuePriority Priority { get; set; }

        [JsonPropertyName("assigned_to")]
        public string AssignedTo { get; set; }

        [JsonPropertyName("resolved_at")]
        public DateTimeOffset? ResolvedAt { get; set; }

        [JsonPropertyName("resolved_by")]
        public string ResolvedBy { get; set; }

        [JsonPropertyName("regression_count")]
        public int RegressionCount { get; set; }

        [JsonPropertyName("last_release")]
        public string LastRelease { get; set; }

        [JsonPropertyName("tags_json")]
        public string TagsJson { get; set; }

        [JsonPropertyName("metadata_json")]
        public string MetadataJson { get; set; }

        [JsonPropertyName("created_at")]
        public DateTimeOffset CreatedAt { get; set; }

        [JsonPropertyName("updated_at")]
        public DateTimeOffset UpdatedAt { get; set; }


    }
}
