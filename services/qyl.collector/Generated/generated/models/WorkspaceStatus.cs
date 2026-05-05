
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qyl.Domains.Workspace
{

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum WorkspaceStatus
    {

        [JsonStringEnumMemberName("active")]
        Active,

        [JsonStringEnumMemberName("suspended")]
        Suspended,

        [JsonStringEnumMemberName("archived")]
        Archived
    }
}
