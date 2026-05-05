
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;

namespace Qyl.Common
{

    public partial class InstrumentationScope
    {
        [JsonPropertyName("name")]
        public string ScopeName { get; set; }

        [StringConstraint(Pattern = @"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-((?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?(?:\+([0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*))?$")]
        [JsonPropertyName("version")]
        public string ScopeVersion { get; set; }

        [JsonPropertyName("attributes")]
        public Attribute[] ScopeAttributes { get; set; }

        [JsonPropertyName("dropped_attributes_count")]
        public long? DroppedAttributesCount { get; set; }


    }
}
