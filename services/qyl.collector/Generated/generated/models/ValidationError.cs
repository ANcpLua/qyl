
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;

namespace Qyl.Common.Errors
{

    public partial class ValidationError : ProblemDetails
    {
        public ValidationError(ValidationErrorDetail[] errors, string title = "Validation Failed") : base(400,
           value: new { errors = errors, title = title })
        {
            Errors = errors;
            Title = title;
        }
        public new string Title { get; } = "Validation Failed";

        public ValidationErrorDetail[] Errors { get; set; }


    }
}
