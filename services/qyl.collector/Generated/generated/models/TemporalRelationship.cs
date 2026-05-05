
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qyl.Domains.Observe.Error
{

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum TemporalRelationship
    {

        [JsonStringEnumMemberName("concurrent")]
        Concurrent,

        [JsonStringEnumMemberName("precedes")]
        Precedes,

        [JsonStringEnumMemberName("follows")]
        Follows,

        [JsonStringEnumMemberName("unrelated")]
        Unrelated
    }
}
