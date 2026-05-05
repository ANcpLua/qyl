
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;

namespace Qyl.Common.Errors
{

    public partial class NotFoundError : ProblemDetails
    {
        public NotFoundError(string title = "Not Found", string resourceType = default, string resourceId = default) : base(404,
           value: new { title = title, resourceType = resourceType, resourceId = resourceId })
        {
            Title = title;
            ResourceType = resourceType;
            ResourceId = resourceId;
        }
        public new string Title { get; } = "Not Found";

        [JsonPropertyName("resource_type")]
        public string ResourceType { get; set; }

        [JsonPropertyName("resource_id")]
        public string ResourceId { get; set; }


    }
}
