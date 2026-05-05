
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qyl.Domains.Issues
{

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum BreadcrumbType
    {

        [JsonStringEnumMemberName("navigation")]
        Navigation,

        [JsonStringEnumMemberName("http")]
        Http,

        [JsonStringEnumMemberName("query")]
        Query,

        [JsonStringEnumMemberName("user")]
        User,

        [JsonStringEnumMemberName("log")]
        Log,

        [JsonStringEnumMemberName("error")]
        Error,

        [JsonStringEnumMemberName("debug")]
        Debug,

        [JsonStringEnumMemberName("default")]
        DefaultName
    }
}
