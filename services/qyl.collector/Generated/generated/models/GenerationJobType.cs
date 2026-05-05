
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qyl.Domains.Configurator
{

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum GenerationJobType
    {

        [JsonStringEnumMemberName("full")]
        Full,

        [JsonStringEnumMemberName("incremental")]
        Incremental,

        [JsonStringEnumMemberName("preview")]
        Preview
    }
}
