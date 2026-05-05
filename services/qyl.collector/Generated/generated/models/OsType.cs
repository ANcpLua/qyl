
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qyl.OTel.Resource
{

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum OsType
    {

        [JsonStringEnumMemberName("windows")]
        Windows,

        [JsonStringEnumMemberName("linux")]
        Linux,

        [JsonStringEnumMemberName("darwin")]
        Darwin,

        [JsonStringEnumMemberName("freebsd")]
        Freebsd,

        [JsonStringEnumMemberName("netbsd")]
        Netbsd,

        [JsonStringEnumMemberName("openbsd")]
        Openbsd,

        [JsonStringEnumMemberName("dragonflybsd")]
        Dragonflybsd,

        [JsonStringEnumMemberName("hpux")]
        Hpux,

        [JsonStringEnumMemberName("aix")]
        Aix,

        [JsonStringEnumMemberName("solaris")]
        Solaris,

        [JsonStringEnumMemberName("z_os")]
        ZOs
    }
}
