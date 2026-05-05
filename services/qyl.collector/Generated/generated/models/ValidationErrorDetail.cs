
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;

namespace Qyl.Common.Errors
{

    public partial class ValidationErrorDetail
    {
        [JsonPropertyName("field")]
        public string FieldName { get; set; }

        public string Message { get; set; }

        public string Code { get; set; }

        [JsonPropertyName("rejected_value")]
        public string RejectedValue { get; set; }


    }
}
