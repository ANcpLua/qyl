
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;

namespace Qyl.Domains.Observe.Session
{

    public partial class SessionGeoInfo
    {
        [JsonPropertyName("country_code")]
        public string CountryCode { get; set; }

        [JsonPropertyName("country_name")]
        public string CountryName { get; set; }

        public string Region { get; set; }

        public string City { get; set; }

        [JsonPropertyName("postal_code")]
        public string PostalCode { get; set; }

        public string Timezone { get; set; }


    }
}
