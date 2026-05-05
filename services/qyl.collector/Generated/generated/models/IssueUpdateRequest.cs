
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;
using Qyl.Domains.Issues;

namespace Qyl.Api
{

    public partial class IssueUpdateRequest
    {
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public IssueStatus? Status { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public IssuePriority? Priority { get; set; }

        [JsonPropertyName("assigned_to")]
        public string AssignedTo { get; set; }


    }
}
