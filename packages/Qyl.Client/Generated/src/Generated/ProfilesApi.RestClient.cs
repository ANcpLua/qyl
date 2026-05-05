
#nullable disable

using System.ClientModel.Primitives;
using Qyl.Client;

namespace Qyl.Api
{
    public partial class ProfilesApi
    {
        private static PipelineMessageClassifier _pipelineMessageClassifier200;

        private static PipelineMessageClassifier PipelineMessageClassifier200 => _pipelineMessageClassifier200 ??= PipelineMessageClassifier.Create(stackalloc ushort[] { 200 });

        internal PipelineMessage CreateGetAllRequest(string sessionId, string traceId, string serviceName, string sampleType, int? limit, RequestOptions options)
        {
            ClientUriBuilder uri = new ClientUriBuilder();
            uri.Reset(_endpoint);
            uri.AppendPath("/api/v1/profiles", false);
            if (sessionId != null)
            {
                uri.AppendQuery("sessionId", sessionId, true);
            }
            if (traceId != null)
            {
                uri.AppendQuery("traceId", traceId, true);
            }
            if (serviceName != null)
            {
                uri.AppendQuery("serviceName", serviceName, true);
            }
            if (sampleType != null)
            {
                uri.AppendQuery("sampleType", sampleType, true);
            }
            if (limit != null)
            {
                uri.AppendQuery("limit", TypeFormatters.ConvertToString(limit), true);
            }
            PipelineMessage message = Pipeline.CreateMessage(uri.ToUri(), "GET", PipelineMessageClassifier200);
            PipelineRequest request = message.Request;
            request.Headers.Set("Accept", "application/json");
            message.Apply(options);
            return message;
        }

        internal PipelineMessage CreateGetRequest(string profileId, RequestOptions options)
        {
            ClientUriBuilder uri = new ClientUriBuilder();
            uri.Reset(_endpoint);
            uri.AppendPath("/api/v1/profiles/", false);
            uri.AppendPath(profileId, true);
            PipelineMessage message = Pipeline.CreateMessage(uri.ToUri(), "GET", PipelineMessageClassifier200);
            PipelineRequest request = message.Request;
            request.Headers.Set("Accept", "application/json");
            message.Apply(options);
            return message;
        }

        internal PipelineMessage CreateGetByTraceRequest(string traceId, int? limit, RequestOptions options)
        {
            ClientUriBuilder uri = new ClientUriBuilder();
            uri.Reset(_endpoint);
            uri.AppendPath("/api/v1/profiles/by-trace/", false);
            uri.AppendPath(traceId, true);
            if (limit != null)
            {
                uri.AppendQuery("limit", TypeFormatters.ConvertToString(limit), true);
            }
            PipelineMessage message = Pipeline.CreateMessage(uri.ToUri(), "GET", PipelineMessageClassifier200);
            PipelineRequest request = message.Request;
            request.Headers.Set("Accept", "application/json");
            message.Apply(options);
            return message;
        }

        internal PipelineMessage CreateGetBySpanRequest(string spanId, int? limit, RequestOptions options)
        {
            ClientUriBuilder uri = new ClientUriBuilder();
            uri.Reset(_endpoint);
            uri.AppendPath("/api/v1/profiles/by-span/", false);
            uri.AppendPath(spanId, true);
            if (limit != null)
            {
                uri.AppendQuery("limit", TypeFormatters.ConvertToString(limit), true);
            }
            PipelineMessage message = Pipeline.CreateMessage(uri.ToUri(), "GET", PipelineMessageClassifier200);
            PipelineRequest request = message.Request;
            request.Headers.Set("Accept", "application/json");
            message.Apply(options);
            return message;
        }
    }
}
