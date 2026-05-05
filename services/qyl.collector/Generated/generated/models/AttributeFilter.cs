
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;

namespace Qyl.Domains.Observe.Log
{

    public partial class AttributeFilter
    {
        public string Key { get; set; }

        [JsonPropertyName("operator")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public FilterOperator OperatorName { get; set; }

        [JsonPropertyName("value")]
        public string ValueName { get; set; }


    }
}
