
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;

namespace Qyl.Domains.Observe.Error
{

    public partial class ErrorCategoryStats
    {
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ErrorCategory Category { get; set; }

        public long Count { get; set; }

        public double Percentage { get; set; }


    }
}
