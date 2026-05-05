
#nullable disable

using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using Qyl.Client;

namespace Qyl.Api
{
    public partial class DeploymentsApi
    {
        private static PipelineMessageClassifier _pipelineMessageClassifier200;

        private static PipelineMessageClassifier PipelineMessageClassifier200 => _pipelineMessageClassifier200 ??= PipelineMessageClassifier.Create(stackalloc ushort[] { 200 });

        internal PipelineMessage CreateGetAllRequest(string serviceName, string environment, string status, DateTimeOffset? startTime, DateTimeOffset? endTime, int? limit, string cursor, RequestOptions options)
        {
            ClientUriBuilder uri = new ClientUriBuilder();
            uri.Reset(_endpoint);
            uri.AppendPath("/api/v1/deployments", false);
            if (serviceName != null)
            {
                uri.AppendQuery("serviceName", serviceName, true);
            }
            if (environment != null)
            {
                uri.AppendQuery("environment", environment, true);
            }
            if (status != null)
            {
                uri.AppendQuery("status", status, true);
            }
            if (startTime != null)
            {
                uri.AppendQuery("startTime", TypeFormatters.ConvertToString(startTime, SerializationFormat.DateTime_RFC3339), true);
            }
            if (endTime != null)
            {
                uri.AppendQuery("endTime", TypeFormatters.ConvertToString(endTime, SerializationFormat.DateTime_RFC3339), true);
            }
            if (limit != null)
            {
                uri.AppendQuery("limit", TypeFormatters.ConvertToString(limit), true);
            }
            if (cursor != null)
            {
                uri.AppendQuery("cursor", cursor, true);
            }
            PipelineMessage message = Pipeline.CreateMessage(uri.ToUri(), "GET", PipelineMessageClassifier200);
            PipelineRequest request = message.Request;
            request.Headers.Set("Accept", "application/json");
            message.Apply(options);
            return message;
        }

        internal PipelineMessage CreateGetRequest(string deploymentId, RequestOptions options)
        {
            ClientUriBuilder uri = new ClientUriBuilder();
            uri.Reset(_endpoint);
            uri.AppendPath("/api/v1/deployments/", false);
            uri.AppendPath(deploymentId, true);
            PipelineMessage message = Pipeline.CreateMessage(uri.ToUri(), "GET", PipelineMessageClassifier200);
            PipelineRequest request = message.Request;
            request.Headers.Set("Accept", "application/json");
            message.Apply(options);
            return message;
        }

        internal PipelineMessage CreateCreateRequest(BinaryContent content, RequestOptions options)
        {
            ClientUriBuilder uri = new ClientUriBuilder();
            uri.Reset(_endpoint);
            uri.AppendPath("/api/v1/deployments", false);
            PipelineMessage message = Pipeline.CreateMessage(uri.ToUri(), "POST", PipelineMessageClassifier200);
            PipelineRequest request = message.Request;
            request.Headers.Set("Content-Type", "application/json");
            request.Headers.Set("Accept", "application/json");
            request.Content = content;
            message.Apply(options);
            return message;
        }

        internal PipelineMessage CreateUpdateRequest(string deploymentId, BinaryContent content, RequestOptions options)
        {
            ClientUriBuilder uri = new ClientUriBuilder();
            uri.Reset(_endpoint);
            uri.AppendPath("/api/v1/deployments/", false);
            uri.AppendPath(deploymentId, true);
            PipelineMessage message = Pipeline.CreateMessage(uri.ToUri(), "PATCH", PipelineMessageClassifier200);
            PipelineRequest request = message.Request;
            request.Headers.Set("Content-Type", "application/json");
            request.Headers.Set("Accept", "application/json");
            request.Content = content;
            message.Apply(options);
            return message;
        }

        internal PipelineMessage CreateGetDoraMetricsRequest(string serviceName, string environment, DateTimeOffset? startTime, DateTimeOffset? endTime, RequestOptions options)
        {
            ClientUriBuilder uri = new ClientUriBuilder();
            uri.Reset(_endpoint);
            uri.AppendPath("/api/v1/deployments/metrics/dora", false);
            if (serviceName != null)
            {
                uri.AppendQuery("serviceName", serviceName, true);
            }
            if (environment != null)
            {
                uri.AppendQuery("environment", environment, true);
            }
            if (startTime != null)
            {
                uri.AppendQuery("startTime", TypeFormatters.ConvertToString(startTime, SerializationFormat.DateTime_RFC3339), true);
            }
            if (endTime != null)
            {
                uri.AppendQuery("endTime", TypeFormatters.ConvertToString(endTime, SerializationFormat.DateTime_RFC3339), true);
            }
            PipelineMessage message = Pipeline.CreateMessage(uri.ToUri(), "GET", PipelineMessageClassifier200);
            PipelineRequest request = message.Request;
            request.Headers.Set("Accept", "application/json");
            message.Apply(options);
            return message;
        }
    }
}
