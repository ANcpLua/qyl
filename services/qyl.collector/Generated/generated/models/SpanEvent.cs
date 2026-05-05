
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;
using Qyl.Common;

namespace Qyl.OTel.Traces
{

    public partial class SpanEvent
    {
        [StringConstraint(MinLength = 1)]
        public string Name { get; set; }

        [JsonPropertyName("time_unix_nano")]
        public long TimeUnixNano { get; set; }

        public Attribute[] Attributes { get; set; }

        [JsonPropertyName("dropped_attributes_count")]
        public long? DroppedAttributesCount { get; set; }


    }
}
