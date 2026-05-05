
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;

namespace Qyl.Common.Errors
{

    public partial class ProblemDetails : HttpServiceException
    {
        public ProblemDetails(string title, int status, string problemType = "about:blank", string detail = default, string instance = default, string traceId = default, string requestId = default, DateTimeOffset timestamp = default) : base(400,
           headers: new() { { "X-Trace-Id", traceId }, { "X-Request-Id", requestId } },
           value: new { title = title, status = status, problemType = problemType, detail = detail, instance = instance, timestamp = timestamp })
        {
            Title = title;
            Status = status;
            ProblemType = problemType;
            Detail = detail;
            Instance = instance;
            TraceId = traceId;
            RequestId = requestId;
            Timestamp = timestamp;
        }
        public ProblemDetails(int statusCode, object? value = null, Dictionary<string, string>? headers = default) : base(statusCode, value, headers) { }

        [JsonPropertyName("type")]
        public string ProblemType { get; set; } = "about:blank";

        public string Title { get; set; }

        public int Status { get; set; }

        public string Detail { get; set; }

        public string Instance { get; set; }

        [JsonPropertyName("trace_id")]
        public string TraceId { get; set; }

        [JsonPropertyName("request_id")]
        public string RequestId { get; set; }

        public DateTimeOffset? Timestamp { get; set; }


    }
}
