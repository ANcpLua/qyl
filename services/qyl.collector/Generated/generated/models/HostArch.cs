
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qyl.OTel.Resource
{

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum HostArch
    {

        [JsonStringEnumMemberName("amd64")]
        Amd64,

        [JsonStringEnumMemberName("arm32")]
        Arm32,

        [JsonStringEnumMemberName("arm64")]
        Arm64,

        [JsonStringEnumMemberName("ia64")]
        Ia64,

        [JsonStringEnumMemberName("ppc32")]
        Ppc32,

        [JsonStringEnumMemberName("ppc64")]
        Ppc64,

        [JsonStringEnumMemberName("s390x")]
        S390x,

        [JsonStringEnumMemberName("x86")]
        X86
    }
}
