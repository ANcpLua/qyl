
#nullable disable

using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Qyl.Client;
using Qyl.Domains.Search;

namespace Qyl.Api
{
    public partial class SearchApi
    {
        private readonly Uri _endpoint;

        protected SearchApi()
        {
        }

        internal SearchApi(ClientPipeline pipeline, Uri endpoint)
        {
            _endpoint = endpoint;
            Pipeline = pipeline;
        }

        public ClientPipeline Pipeline { get; }

        public virtual ClientResult Search(BinaryContent content, RequestOptions options = null)
        {
            Argument.AssertNotNull(content, nameof(content));

            using PipelineMessage message = CreateSearchRequest(content, options);
            return ClientResult.FromResponse(Pipeline.ProcessMessage(message, options));
        }

        public virtual async Task<ClientResult> SearchAsync(BinaryContent content, RequestOptions options = null)
        {
            Argument.AssertNotNull(content, nameof(content));

            using PipelineMessage message = CreateSearchRequest(content, options);
            return ClientResult.FromResponse(await Pipeline.ProcessMessageAsync(message, options).ConfigureAwait(false));
        }

        public virtual ClientResult<SearchResponse> Search(SearchRequest request, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNull(request, nameof(request));

            ClientResult result = Search(request, cancellationToken.ToRequestOptions());
            return ClientResult.FromValue((SearchResponse)result, result.GetRawResponse());
        }

        public virtual async Task<ClientResult<SearchResponse>> SearchAsync(SearchRequest request, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNull(request, nameof(request));

            ClientResult result = await SearchAsync(request, cancellationToken.ToRequestOptions()).ConfigureAwait(false);
            return ClientResult.FromValue((SearchResponse)result, result.GetRawResponse());
        }

        public virtual ClientResult GetSuggestions(string query, int? limit, RequestOptions options)
        {
            Argument.AssertNotNullOrEmpty(query, nameof(query));

            using PipelineMessage message = CreateGetSuggestionsRequest(query, limit, options);
            return ClientResult.FromResponse(Pipeline.ProcessMessage(message, options));
        }

        public virtual async Task<ClientResult> GetSuggestionsAsync(string query, int? limit, RequestOptions options)
        {
            Argument.AssertNotNullOrEmpty(query, nameof(query));

            using PipelineMessage message = CreateGetSuggestionsRequest(query, limit, options);
            return ClientResult.FromResponse(await Pipeline.ProcessMessageAsync(message, options).ConfigureAwait(false));
        }

        public virtual ClientResult<IReadOnlyList<string>> GetSuggestions(string query, int? limit = default, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNullOrEmpty(query, nameof(query));

            ClientResult result = GetSuggestions(query, limit, cancellationToken.ToRequestOptions());
            List<string> value = new List<string>();
            BinaryData data = result.GetRawResponse().Content;
            Utf8JsonReader jsonReader = new Utf8JsonReader(data.ToMemory().Span);
            jsonReader.Read();
            while (jsonReader.Read())
            {
                if (jsonReader.TokenType == JsonTokenType.EndArray)
                {
                    break;
                }
                value.Add(jsonReader.GetString());
            }
            return ClientResult.FromValue((IReadOnlyList<string>)value, result.GetRawResponse());
        }

        public virtual async Task<ClientResult<IReadOnlyList<string>>> GetSuggestionsAsync(string query, int? limit = default, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNullOrEmpty(query, nameof(query));

            ClientResult result = await GetSuggestionsAsync(query, limit, cancellationToken.ToRequestOptions()).ConfigureAwait(false);
            List<string> value = new List<string>();
            BinaryData data = result.GetRawResponse().Content;
            Utf8JsonReader jsonReader = new Utf8JsonReader(data.ToMemory().Span);
            jsonReader.Read();
            while (jsonReader.Read())
            {
                if (jsonReader.TokenType == JsonTokenType.EndArray)
                {
                    break;
                }
                value.Add(jsonReader.GetString());
            }
            return ClientResult.FromValue((IReadOnlyList<string>)value, result.GetRawResponse());
        }
    }
}
