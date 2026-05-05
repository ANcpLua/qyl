
#nullable disable

using System.ClientModel;
using System.ClientModel.Primitives;
using Qyl.Client;

namespace Qyl.Api
{
    public partial class ConfiguratorApi
    {
        private static PipelineMessageClassifier _pipelineMessageClassifier200;

        private static PipelineMessageClassifier PipelineMessageClassifier200 => _pipelineMessageClassifier200 ??= PipelineMessageClassifier.Create(stackalloc ushort[] { 200 });

        internal PipelineMessage CreateGetProfilesRequest(int? limit, string cursor, RequestOptions options)
        {
            ClientUriBuilder uri = new ClientUriBuilder();
            uri.Reset(_endpoint);
            uri.AppendPath("/api/v1/configurator/profiles", false);
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

        internal PipelineMessage CreateGetProfileRequest(string profileId, RequestOptions options)
        {
            ClientUriBuilder uri = new ClientUriBuilder();
            uri.Reset(_endpoint);
            uri.AppendPath("/api/v1/configurator/profiles/", false);
            uri.AppendPath(profileId, true);
            PipelineMessage message = Pipeline.CreateMessage(uri.ToUri(), "GET", PipelineMessageClassifier200);
            PipelineRequest request = message.Request;
            request.Headers.Set("Accept", "application/json");
            message.Apply(options);
            return message;
        }

        internal PipelineMessage CreateCreateProfileRequest(BinaryContent content, RequestOptions options)
        {
            ClientUriBuilder uri = new ClientUriBuilder();
            uri.Reset(_endpoint);
            uri.AppendPath("/api/v1/configurator/profiles", false);
            PipelineMessage message = Pipeline.CreateMessage(uri.ToUri(), "POST", PipelineMessageClassifier200);
            PipelineRequest request = message.Request;
            request.Headers.Set("Content-Type", "application/json");
            request.Headers.Set("Accept", "application/json");
            request.Content = content;
            message.Apply(options);
            return message;
        }

        internal PipelineMessage CreateGetSelectionsRequest(string workspaceId, RequestOptions options)
        {
            ClientUriBuilder uri = new ClientUriBuilder();
            uri.Reset(_endpoint);
            uri.AppendPath("/api/v1/configurator/selections", false);
            uri.AppendQuery("workspaceId", workspaceId, true);
            PipelineMessage message = Pipeline.CreateMessage(uri.ToUri(), "GET", PipelineMessageClassifier200);
            PipelineRequest request = message.Request;
            request.Headers.Set("Accept", "application/json");
            message.Apply(options);
            return message;
        }

        internal PipelineMessage CreateSaveSelectionsRequest(BinaryContent content, RequestOptions options)
        {
            ClientUriBuilder uri = new ClientUriBuilder();
            uri.Reset(_endpoint);
            uri.AppendPath("/api/v1/configurator/selections", false);
            PipelineMessage message = Pipeline.CreateMessage(uri.ToUri(), "POST", PipelineMessageClassifier200);
            PipelineRequest request = message.Request;
            request.Headers.Set("Content-Type", "application/json");
            request.Headers.Set("Accept", "application/json");
            request.Content = content;
            message.Apply(options);
            return message;
        }

        internal PipelineMessage CreateCreateJobRequest(BinaryContent content, RequestOptions options)
        {
            ClientUriBuilder uri = new ClientUriBuilder();
            uri.Reset(_endpoint);
            uri.AppendPath("/api/v1/configurator/jobs", false);
            PipelineMessage message = Pipeline.CreateMessage(uri.ToUri(), "POST", PipelineMessageClassifier200);
            PipelineRequest request = message.Request;
            request.Headers.Set("Content-Type", "application/json");
            request.Headers.Set("Accept", "application/json");
            request.Content = content;
            message.Apply(options);
            return message;
        }

        internal PipelineMessage CreateGetJobRequest(string jobId, RequestOptions options)
        {
            ClientUriBuilder uri = new ClientUriBuilder();
            uri.Reset(_endpoint);
            uri.AppendPath("/api/v1/configurator/jobs/", false);
            uri.AppendPath(jobId, true);
            PipelineMessage message = Pipeline.CreateMessage(uri.ToUri(), "GET", PipelineMessageClassifier200);
            PipelineRequest request = message.Request;
            request.Headers.Set("Accept", "application/json");
            message.Apply(options);
            return message;
        }
    }
}
