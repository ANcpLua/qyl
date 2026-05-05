
#nullable disable

using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Threading;
using System.Threading.Tasks;
using Qyl.Client;
using Qyl.Common.Pagination;
using Qyl.OTel.Enums;
using Qyl.OTel.Traces;
using Trace = Qyl.OTel.Traces.Trace;

namespace Qyl.Api
{
    public partial class TracesApi
    {
        private readonly Uri _endpoint;

        protected TracesApi()
        {
        }

        internal TracesApi(ClientPipeline pipeline, Uri endpoint)
        {
            _endpoint = endpoint;
            Pipeline = pipeline;
        }

        public ClientPipeline Pipeline { get; }

        public virtual ClientResult GetAll(string serviceName, long? minDurationMs, long? maxDurationMs, int? status, DateTimeOffset? startTime, DateTimeOffset? endTime, int? limit, string cursor, RequestOptions options)
        {
            using PipelineMessage message = this.CreateGetAllRequest(serviceName, minDurationMs, maxDurationMs, status, startTime, endTime, limit, cursor, options);
            return ClientResult.FromResponse(Pipeline.ProcessMessage(message, options));
        }

        public virtual async Task<ClientResult> GetAllAsync(string serviceName, long? minDurationMs, long? maxDurationMs, int? status, DateTimeOffset? startTime, DateTimeOffset? endTime, int? limit, string cursor, RequestOptions options)
        {
            using PipelineMessage message = this.CreateGetAllRequest(serviceName, minDurationMs, maxDurationMs, status, startTime, endTime, limit, cursor, options);
            return ClientResult.FromResponse(await Pipeline.ProcessMessageAsync(message, options).ConfigureAwait(false));
        }

        public virtual ClientResult<CursorPageTrace> GetAll(string serviceName = default, long? minDurationMs = default, long? maxDurationMs = default, SpanStatusCode? status = default, DateTimeOffset? startTime = default, DateTimeOffset? endTime = default, int? limit = default, string cursor = default, CancellationToken cancellationToken = default)
        {
            ClientResult result = this.GetAll(serviceName, minDurationMs, maxDurationMs, (int?)status, startTime, endTime, limit, cursor, cancellationToken.ToRequestOptions());
            return ClientResult.FromValue((CursorPageTrace)result, result.GetRawResponse());
        }

        public virtual async Task<ClientResult<CursorPageTrace>> GetAllAsync(string serviceName = default, long? minDurationMs = default, long? maxDurationMs = default, SpanStatusCode? status = default, DateTimeOffset? startTime = default, DateTimeOffset? endTime = default, int? limit = default, string cursor = default, CancellationToken cancellationToken = default)
        {
            ClientResult result = await this.GetAllAsync(serviceName, minDurationMs, maxDurationMs, (int?)status, startTime, endTime, limit, cursor, cancellationToken.ToRequestOptions()).ConfigureAwait(false);
            return ClientResult.FromValue((CursorPageTrace)result, result.GetRawResponse());
        }

        public virtual ClientResult Get(string traceId, RequestOptions options)
        {
            Argument.AssertNotNullOrEmpty(traceId, nameof(traceId));

            using PipelineMessage message = CreateGetRequest(traceId, options);
            return ClientResult.FromResponse(Pipeline.ProcessMessage(message, options));
        }

        public virtual async Task<ClientResult> GetAsync(string traceId, RequestOptions options)
        {
            Argument.AssertNotNullOrEmpty(traceId, nameof(traceId));

            using PipelineMessage message = CreateGetRequest(traceId, options);
            return ClientResult.FromResponse(await Pipeline.ProcessMessageAsync(message, options).ConfigureAwait(false));
        }

        public virtual ClientResult<Trace> Get(string traceId, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNullOrEmpty(traceId, nameof(traceId));

            ClientResult result = Get(traceId, cancellationToken.ToRequestOptions());
            return ClientResult.FromValue((Trace)result, result.GetRawResponse());
        }

        public virtual async Task<ClientResult<Trace>> GetAsync(string traceId, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNullOrEmpty(traceId, nameof(traceId));

            ClientResult result = await GetAsync(traceId, cancellationToken.ToRequestOptions()).ConfigureAwait(false);
            return ClientResult.FromValue((Trace)result, result.GetRawResponse());
        }

        public virtual ClientResult GetSpans(string traceId, int? limit, string cursor, RequestOptions options)
        {
            Argument.AssertNotNullOrEmpty(traceId, nameof(traceId));

            using PipelineMessage message = CreateGetSpansRequest(traceId, limit, cursor, options);
            return ClientResult.FromResponse(Pipeline.ProcessMessage(message, options));
        }

        public virtual async Task<ClientResult> GetSpansAsync(string traceId, int? limit, string cursor, RequestOptions options)
        {
            Argument.AssertNotNullOrEmpty(traceId, nameof(traceId));

            using PipelineMessage message = CreateGetSpansRequest(traceId, limit, cursor, options);
            return ClientResult.FromResponse(await Pipeline.ProcessMessageAsync(message, options).ConfigureAwait(false));
        }

        public virtual ClientResult<CursorPageSpanRecord> GetSpans(string traceId, int? limit = default, string cursor = default, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNullOrEmpty(traceId, nameof(traceId));

            ClientResult result = GetSpans(traceId, limit, cursor, cancellationToken.ToRequestOptions());
            return ClientResult.FromValue((CursorPageSpanRecord)result, result.GetRawResponse());
        }

        public virtual async Task<ClientResult<CursorPageSpanRecord>> GetSpansAsync(string traceId, int? limit = default, string cursor = default, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNullOrEmpty(traceId, nameof(traceId));

            ClientResult result = await GetSpansAsync(traceId, limit, cursor, cancellationToken.ToRequestOptions()).ConfigureAwait(false);
            return ClientResult.FromValue((CursorPageSpanRecord)result, result.GetRawResponse());
        }

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

        public virtual ClientResult<CursorPageTrace> Search(TraceQuery query, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNull(query, nameof(query));

            ClientResult result = Search(query, cancellationToken.ToRequestOptions());
            return ClientResult.FromValue((CursorPageTrace)result, result.GetRawResponse());
        }

        public virtual async Task<ClientResult<CursorPageTrace>> SearchAsync(TraceQuery query, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNull(query, nameof(query));

            ClientResult result = await SearchAsync(query, cancellationToken.ToRequestOptions()).ConfigureAwait(false);
            return ClientResult.FromValue((CursorPageTrace)result, result.GetRawResponse());
        }
    }
}
