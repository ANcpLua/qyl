
#nullable disable

using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Threading;
using System.Threading.Tasks;
using Qyl.Client;
using Qyl.Common.Pagination;
using Qyl.Domains.Observe.Session;

namespace Qyl.Api
{
    public partial class SessionsApi
    {
        private readonly Uri _endpoint;

        protected SessionsApi()
        {
        }

        internal SessionsApi(ClientPipeline pipeline, Uri endpoint)
        {
            _endpoint = endpoint;
            Pipeline = pipeline;
        }

        public ClientPipeline Pipeline { get; }

        public virtual ClientResult GetAll(string userId, bool? isActive, DateTimeOffset? startTime, DateTimeOffset? endTime, int? limit, string cursor, RequestOptions options)
        {
            using PipelineMessage message = CreateGetAllRequest(userId, isActive, startTime, endTime, limit, cursor, options);
            return ClientResult.FromResponse(Pipeline.ProcessMessage(message, options));
        }

        public virtual async Task<ClientResult> GetAllAsync(string userId, bool? isActive, DateTimeOffset? startTime, DateTimeOffset? endTime, int? limit, string cursor, RequestOptions options)
        {
            using PipelineMessage message = CreateGetAllRequest(userId, isActive, startTime, endTime, limit, cursor, options);
            return ClientResult.FromResponse(await Pipeline.ProcessMessageAsync(message, options).ConfigureAwait(false));
        }

        public virtual ClientResult<CursorPageSessionEntity> GetAll(string userId = default, bool? isActive = default, DateTimeOffset? startTime = default, DateTimeOffset? endTime = default, int? limit = default, string cursor = default, CancellationToken cancellationToken = default)
        {
            ClientResult result = GetAll(userId, isActive, startTime, endTime, limit, cursor, cancellationToken.ToRequestOptions());
            return ClientResult.FromValue((CursorPageSessionEntity)result, result.GetRawResponse());
        }

        public virtual async Task<ClientResult<CursorPageSessionEntity>> GetAllAsync(string userId = default, bool? isActive = default, DateTimeOffset? startTime = default, DateTimeOffset? endTime = default, int? limit = default, string cursor = default, CancellationToken cancellationToken = default)
        {
            ClientResult result = await GetAllAsync(userId, isActive, startTime, endTime, limit, cursor, cancellationToken.ToRequestOptions()).ConfigureAwait(false);
            return ClientResult.FromValue((CursorPageSessionEntity)result, result.GetRawResponse());
        }

        public virtual ClientResult Get(string sessionId, RequestOptions options)
        {
            Argument.AssertNotNullOrEmpty(sessionId, nameof(sessionId));

            using PipelineMessage message = CreateGetRequest(sessionId, options);
            return ClientResult.FromResponse(Pipeline.ProcessMessage(message, options));
        }

        public virtual async Task<ClientResult> GetAsync(string sessionId, RequestOptions options)
        {
            Argument.AssertNotNullOrEmpty(sessionId, nameof(sessionId));

            using PipelineMessage message = CreateGetRequest(sessionId, options);
            return ClientResult.FromResponse(await Pipeline.ProcessMessageAsync(message, options).ConfigureAwait(false));
        }

        public virtual ClientResult<SessionEntity> Get(string sessionId, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNullOrEmpty(sessionId, nameof(sessionId));

            ClientResult result = Get(sessionId, cancellationToken.ToRequestOptions());
            return ClientResult.FromValue((SessionEntity)result, result.GetRawResponse());
        }

        public virtual async Task<ClientResult<SessionEntity>> GetAsync(string sessionId, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNullOrEmpty(sessionId, nameof(sessionId));

            ClientResult result = await GetAsync(sessionId, cancellationToken.ToRequestOptions()).ConfigureAwait(false);
            return ClientResult.FromValue((SessionEntity)result, result.GetRawResponse());
        }

        public virtual ClientResult GetTraces(string sessionId, int? limit, string cursor, RequestOptions options)
        {
            Argument.AssertNotNullOrEmpty(sessionId, nameof(sessionId));

            using PipelineMessage message = CreateGetTracesRequest(sessionId, limit, cursor, options);
            return ClientResult.FromResponse(Pipeline.ProcessMessage(message, options));
        }

        public virtual async Task<ClientResult> GetTracesAsync(string sessionId, int? limit, string cursor, RequestOptions options)
        {
            Argument.AssertNotNullOrEmpty(sessionId, nameof(sessionId));

            using PipelineMessage message = CreateGetTracesRequest(sessionId, limit, cursor, options);
            return ClientResult.FromResponse(await Pipeline.ProcessMessageAsync(message, options).ConfigureAwait(false));
        }

        public virtual ClientResult<CursorPageTrace> GetTraces(string sessionId, int? limit = default, string cursor = default, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNullOrEmpty(sessionId, nameof(sessionId));

            ClientResult result = GetTraces(sessionId, limit, cursor, cancellationToken.ToRequestOptions());
            return ClientResult.FromValue((CursorPageTrace)result, result.GetRawResponse());
        }

        public virtual async Task<ClientResult<CursorPageTrace>> GetTracesAsync(string sessionId, int? limit = default, string cursor = default, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNullOrEmpty(sessionId, nameof(sessionId));

            ClientResult result = await GetTracesAsync(sessionId, limit, cursor, cancellationToken.ToRequestOptions()).ConfigureAwait(false);
            return ClientResult.FromValue((CursorPageTrace)result, result.GetRawResponse());
        }

        public virtual ClientResult GetStats(DateTimeOffset? startTime, DateTimeOffset? endTime, RequestOptions options)
        {
            using PipelineMessage message = CreateGetStatsRequest(startTime, endTime, options);
            return ClientResult.FromResponse(Pipeline.ProcessMessage(message, options));
        }

        public virtual async Task<ClientResult> GetStatsAsync(DateTimeOffset? startTime, DateTimeOffset? endTime, RequestOptions options)
        {
            using PipelineMessage message = CreateGetStatsRequest(startTime, endTime, options);
            return ClientResult.FromResponse(await Pipeline.ProcessMessageAsync(message, options).ConfigureAwait(false));
        }

        public virtual ClientResult<SessionStats> GetStats(DateTimeOffset? startTime = default, DateTimeOffset? endTime = default, CancellationToken cancellationToken = default)
        {
            ClientResult result = GetStats(startTime, endTime, cancellationToken.ToRequestOptions());
            return ClientResult.FromValue((SessionStats)result, result.GetRawResponse());
        }

        public virtual async Task<ClientResult<SessionStats>> GetStatsAsync(DateTimeOffset? startTime = default, DateTimeOffset? endTime = default, CancellationToken cancellationToken = default)
        {
            ClientResult result = await GetStatsAsync(startTime, endTime, cancellationToken.ToRequestOptions()).ConfigureAwait(false);
            return ClientResult.FromValue((SessionStats)result, result.GetRawResponse());
        }
    }
}
