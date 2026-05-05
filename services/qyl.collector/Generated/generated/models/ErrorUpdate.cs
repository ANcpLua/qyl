
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;
using Qyl.Domains.Observe.Error;

namespace Qyl.Api
{

    public partial class ErrorUpdate
    {
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ErrorStatus? Status { get; set; }

        [JsonPropertyName("assigned_to")]
        public string AssignedTo { get; set; }

        [JsonPropertyName("issue_url")]
        public string IssueUrl { get; set; }


    }
}
