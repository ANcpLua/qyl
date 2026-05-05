
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;
using Qyl.Common;

namespace Qyl.Domains.Observe.Error
{

    public partial class ErrorCorrelation
    {
        [JsonPropertyName("error_id")]
        public string ErrorId { get; set; }

        [JsonPropertyName("correlated_errors")]
        public CorrelatedError[] CorrelatedErrors { get; set; }

        [JsonPropertyName("root_cause")]
        public string RootCause { get; set; }

        [JsonPropertyName("common_attributes")]
        public Attribute[] CommonAttributes { get; set; }


    }
}
