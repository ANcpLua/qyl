
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;

namespace Qyl.Common.Errors
{

    public partial class InternalServerError : ProblemDetails
    {
        public InternalServerError(string title = "Internal Server Error", string errorCode = default) : base(500,
           value: new { title = title, errorCode = errorCode })
        {
            Title = title;
            ErrorCode = errorCode;
        }
        public new string Title { get; } = "Internal Server Error";

        [JsonPropertyName("error_code")]
        public string ErrorCode { get; set; }


    }
}
