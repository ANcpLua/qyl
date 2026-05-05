
#nullable disable

using System.ClientModel;
using System.ClientModel.Primitives;
using Qyl.Client;

namespace Qyl.Api
{
    public partial class SearchApi
    {
        private static PipelineMessageClassifier _pipelineMessageClassifier200;

        private static PipelineMessageClassifier PipelineMessageClassifier200 => _pipelineMessageClassifier200 ??= PipelineMessageClassifier.Create(stackalloc ushort[] { 200 });

        internal PipelineMessage CreateSearchRequest(BinaryContent content, RequestOptions options)
        {
            ClientUriBuilder uri = new ClientUriBuilder();
            uri.Reset(_endpoint);
            uri.AppendPath("/api/v1/search", false);
            PipelineMessage message = Pipeline.CreateMessage(uri.ToUri(), "POST", PipelineMessageClassifier200);
            PipelineRequest request = message.Request;
            request.Headers.Set("Content-Type", "application/json");
            request.Headers.Set("Accept", "application/json");
            request.Content = content;
            message.Apply(options);
            return message;
        }

        internal PipelineMessage CreateGetSuggestionsRequest(string query, int? limit, RequestOptions options)
        {
            ClientUriBuilder uri = new ClientUriBuilder();
            uri.Reset(_endpoint);
            uri.AppendPath("/api/v1/search/suggestions", false);
            uri.AppendQuery("query", query, true);
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
