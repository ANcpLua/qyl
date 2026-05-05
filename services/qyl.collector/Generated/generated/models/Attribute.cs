
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;

namespace Qyl.Common
{

    public partial class Attribute
    {
        [StringConstraint(MinLength = 1, MaxLength = 256)]
        public string Key { get; set; }

        [JsonPropertyName("value")]
        public object ValueName { get; set; }


    }
}
