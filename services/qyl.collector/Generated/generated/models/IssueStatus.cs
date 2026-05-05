
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qyl.Domains.Issues
{

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum IssueStatus
    {

        [JsonStringEnumMemberName("unresolved")]
        Unresolved,

        [JsonStringEnumMemberName("acknowledged")]
        Acknowledged,

        [JsonStringEnumMemberName("investigating")]
        Investigating,

        [JsonStringEnumMemberName("in_progress")]
        InProgress,

        [JsonStringEnumMemberName("resolved")]
        Resolved,

        [JsonStringEnumMemberName("ignored")]
        Ignored,

        [JsonStringEnumMemberName("regressed")]
        Regressed
    }
}
