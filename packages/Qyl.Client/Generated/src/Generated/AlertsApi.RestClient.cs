
#nullable disable

using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using Qyl.Client;

namespace Qyl.Api
{
    public partial class AlertsApi
    {
        private static PipelineMessageClassifier _pipelineMessageClassifier200;
        private static PipelineMessageClassifier _pipelineMessageClassifier204;

        private static PipelineMessageClassifier PipelineMessageClassifier200 => _pipelineMessageClassifier200 ??= PipelineMessageClassifier.Create(stackalloc ushort[] { 200 });

        private static PipelineMessageClassifier PipelineMessageClassifier204 => _pipelineMessageClassifier204 ??= PipelineMessageClassifier.Create(stackalloc ushort[] { 204 });

        internal PipelineMessage CreateGetRulesRequest(string projectId, bool? enabled, int? limit, string cursor, RequestOptions options)
        {
            ClientUriBuilder uri = new ClientUriBuilder();
            uri.Reset(_endpoint);
            uri.AppendPath("/api/v1/alerts/rules", false);
            if (projectId != null)
            {
                uri.AppendQuery("projectId", projectId, true);
            }
            if (enabled != null)
            {
                uri.AppendQuery("enabled", TypeFormatters.ConvertToString(enabled), true);
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

        internal PipelineMessage CreateGetRuleRequest(string ruleId, RequestOptions options)
        {
            ClientUriBuilder uri = new ClientUriBuilder();
            uri.Reset(_endpoint);
            uri.AppendPath("/api/v1/alerts/rules/", false);
            uri.AppendPath(ruleId, true);
            PipelineMessage message = Pipeline.CreateMessage(uri.ToUri(), "GET", PipelineMessageClassifier200);
            PipelineRequest request = message.Request;
            request.Headers.Set("Accept", "application/json");
            message.Apply(options);
            return message;
        }

        internal PipelineMessage CreateCreateRuleRequest(BinaryContent content, RequestOptions options)
        {
            ClientUriBuilder uri = new ClientUriBuilder();
            uri.Reset(_endpoint);
            uri.AppendPath("/api/v1/alerts/rules", false);
            PipelineMessage message = Pipeline.CreateMessage(uri.ToUri(), "POST", PipelineMessageClassifier200);
            PipelineRequest request = message.Request;
            request.Headers.Set("Content-Type", "application/json");
            request.Headers.Set("Accept", "application/json");
            request.Content = content;
            message.Apply(options);
            return message;
        }

        internal PipelineMessage CreateUpdateRuleRequest(string ruleId, BinaryContent content, RequestOptions options)
        {
            ClientUriBuilder uri = new ClientUriBuilder();
            uri.Reset(_endpoint);
            uri.AppendPath("/api/v1/alerts/rules/", false);
            uri.AppendPath(ruleId, true);
            PipelineMessage message = Pipeline.CreateMessage(uri.ToUri(), "PUT", PipelineMessageClassifier200);
            PipelineRequest request = message.Request;
            request.Headers.Set("Content-Type", "application/json");
            request.Headers.Set("Accept", "application/json");
            request.Content = content;
            message.Apply(options);
            return message;
        }

        internal PipelineMessage CreateDeleteRuleRequest(string ruleId, RequestOptions options)
        {
            ClientUriBuilder uri = new ClientUriBuilder();
            uri.Reset(_endpoint);
            uri.AppendPath("/api/v1/alerts/rules/", false);
            uri.AppendPath(ruleId, true);
            PipelineMessage message = Pipeline.CreateMessage(uri.ToUri(), "DELETE", PipelineMessageClassifier204);
            PipelineRequest request = message.Request;
            message.Apply(options);
            return message;
        }

        internal PipelineMessage CreateGetFiringsRequest(string ruleId, string status, DateTimeOffset? startTime, DateTimeOffset? endTime, int? limit, string cursor, RequestOptions options)
        {
            ClientUriBuilder uri = new ClientUriBuilder();
            uri.Reset(_endpoint);
            uri.AppendPath("/api/v1/alerts/firings", false);
            if (ruleId != null)
            {
                uri.AppendQuery("ruleId", ruleId, true);
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

        internal PipelineMessage CreateAcknowledgeFiringRequest(string firingId, BinaryContent content, RequestOptions options)
        {
            ClientUriBuilder uri = new ClientUriBuilder();
            uri.Reset(_endpoint);
            uri.AppendPath("/api/v1/alerts/firings/", false);
            uri.AppendPath(firingId, true);
            uri.AppendPath("/acknowledge", false);
            PipelineMessage message = Pipeline.CreateMessage(uri.ToUri(), "POST", PipelineMessageClassifier200);
            PipelineRequest request = message.Request;
            request.Headers.Set("Content-Type", "application/json");
            request.Headers.Set("Accept", "application/json");
            request.Content = content;
            message.Apply(options);
            return message;
        }

        internal PipelineMessage CreateResolveFiringRequest(string firingId, RequestOptions options)
        {
            ClientUriBuilder uri = new ClientUriBuilder();
            uri.Reset(_endpoint);
            uri.AppendPath("/api/v1/alerts/firings/", false);
            uri.AppendPath(firingId, true);
            uri.AppendPath("/resolve", false);
            PipelineMessage message = Pipeline.CreateMessage(uri.ToUri(), "POST", PipelineMessageClassifier200);
            PipelineRequest request = message.Request;
            request.Headers.Set("Accept", "application/json");
            message.Apply(options);
            return message;
        }

        internal PipelineMessage CreateGetFixRunsRequest(string issueId, string status, int? limit, string cursor, RequestOptions options)
        {
            ClientUriBuilder uri = new ClientUriBuilder();
            uri.Reset(_endpoint);
            uri.AppendPath("/api/v1/alerts/fixes", false);
            if (issueId != null)
            {
                uri.AppendQuery("issueId", issueId, true);
            }
            if (status != null)
            {
                uri.AppendQuery("status", status, true);
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

        internal PipelineMessage CreateGetFixRunRequest(string fixId, RequestOptions options)
        {
            ClientUriBuilder uri = new ClientUriBuilder();
            uri.Reset(_endpoint);
            uri.AppendPath("/api/v1/alerts/fixes/", false);
            uri.AppendPath(fixId, true);
            PipelineMessage message = Pipeline.CreateMessage(uri.ToUri(), "GET", PipelineMessageClassifier200);
            PipelineRequest request = message.Request;
            request.Headers.Set("Accept", "application/json");
            message.Apply(options);
            return message;
        }
    }
}
