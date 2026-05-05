
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qyl.Domains.Observe.Error
{

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ErrorStatus
    {

        [JsonStringEnumMemberName("new")]
        NewName,

        [JsonStringEnumMemberName("acknowledged")]
        Acknowledged,

        [JsonStringEnumMemberName("in_progress")]
        InProgress,

        [JsonStringEnumMemberName("resolved")]
        Resolved,

        [JsonStringEnumMemberName("ignored")]
        Ignored,

        [JsonStringEnumMemberName("regressed")]
        Regressed,

        [JsonStringEnumMemberName("wont_fix")]
        WontFix
    }
}
