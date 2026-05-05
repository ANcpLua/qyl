
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;

namespace Qyl.Domains.Observe.Session
{

    public partial class SessionClientInfo
    {
        public string Ip { get; set; }

        [JsonPropertyName("user_agent")]
        public string UserAgent { get; set; }

        [JsonPropertyName("device_type")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public DeviceType? DeviceType { get; set; }

        public string Os { get; set; }

        public string Browser { get; set; }

        [JsonPropertyName("browser_version")]
        public string BrowserVersion { get; set; }


    }
}
