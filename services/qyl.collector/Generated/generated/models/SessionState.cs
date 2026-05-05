
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qyl.Domains.Observe.Session
{

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum SessionState
    {

        [JsonStringEnumMemberName("active")]
        Active,

        [JsonStringEnumMemberName("idle")]
        Idle,

        [JsonStringEnumMemberName("ended")]
        Ended,

        [JsonStringEnumMemberName("timed_out")]
        TimedOut,

        [JsonStringEnumMemberName("invalidated")]
        Invalidated
    }
}
