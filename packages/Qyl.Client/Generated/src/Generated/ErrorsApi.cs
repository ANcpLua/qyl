
#nullable disable

using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Threading;
using System.Threading.Tasks;
using Qyl.Client;
using Qyl.Common.Pagination;
using Qyl.Domains.Observe.Error;

namespace Qyl.Api
{
    public partial class ErrorsApi
    {
        private readonly Uri _endpoint;

        protected ErrorsApi()
        {
        }

        internal ErrorsApi(ClientPipeline pipeline, Uri endpoint)
        {
            _endpoint = endpoint;
            Pipeline = pipeline;
        }

        public ClientPipeline Pipeline { get; }

        public virtual ClientResult GetAll(string serviceName, string status, string category, DateTimeOffset? startTime, DateTimeOffset? endTime, int? limit, string cursor, RequestOptions options)
        {
            using PipelineMessage message = CreateGetAllRequest(serviceName, status, category, startTime, endTime, limit, cursor, options);
            return ClientResult.FromResponse(Pipeline.ProcessMessage(message, options));
        }

        public virtual async Task<ClientResult> GetAllAsync(string serviceName, string status, string category, DateTimeOffset? startTime, DateTimeOffset? endTime, int? limit, string cursor, RequestOptions options)
        {
            using PipelineMessage message = CreateGetAllRequest(serviceName, status, category, startTime, endTime, limit, cursor, options);
            return ClientResult.FromResponse(await Pipeline.ProcessMessageAsync(message, options).ConfigureAwait(false));
        }

        public virtual ClientResult<CursorPageErrorEntity> GetAll(string serviceName = default, ErrorStatus? status = default, ErrorCategory? category = default, DateTimeOffset? startTime = default, DateTimeOffset? endTime = default, int? limit = default, string cursor = default, CancellationToken cancellationToken = default)
        {
            ClientResult result = GetAll(serviceName, status?.ToSerialString(), category?.ToSerialString(), startTime, endTime, limit, cursor, cancellationToken.ToRequestOptions());
            return ClientResult.FromValue((CursorPageErrorEntity)result, result.GetRawResponse());
        }

        public virtual async Task<ClientResult<CursorPageErrorEntity>> GetAllAsync(string serviceName = default, ErrorStatus? status = default, ErrorCategory? category = default, DateTimeOffset? startTime = default, DateTimeOffset? endTime = default, int? limit = default, string cursor = default, CancellationToken cancellationToken = default)
        {
            ClientResult result = await GetAllAsync(serviceName, status?.ToSerialString(), category?.ToSerialString(), startTime, endTime, limit, cursor, cancellationToken.ToRequestOptions()).ConfigureAwait(false);
            return ClientResult.FromValue((CursorPageErrorEntity)result, result.GetRawResponse());
        }

        public virtual ClientResult Get(string errorId, RequestOptions options)
        {
            Argument.AssertNotNullOrEmpty(errorId, nameof(errorId));

            using PipelineMessage message = CreateGetRequest(errorId, options);
            return ClientResult.FromResponse(Pipeline.ProcessMessage(message, options));
        }

        public virtual async Task<ClientResult> GetAsync(string errorId, RequestOptions options)
        {
            Argument.AssertNotNullOrEmpty(errorId, nameof(errorId));

            using PipelineMessage message = CreateGetRequest(errorId, options);
            return ClientResult.FromResponse(await Pipeline.ProcessMessageAsync(message, options).ConfigureAwait(false));
        }

        public virtual ClientResult<ErrorEntity> Get(string errorId, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNullOrEmpty(errorId, nameof(errorId));

            ClientResult result = Get(errorId, cancellationToken.ToRequestOptions());
            return ClientResult.FromValue((ErrorEntity)result, result.GetRawResponse());
        }

        public virtual async Task<ClientResult<ErrorEntity>> GetAsync(string errorId, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNullOrEmpty(errorId, nameof(errorId));

            ClientResult result = await GetAsync(errorId, cancellationToken.ToRequestOptions()).ConfigureAwait(false);
            return ClientResult.FromValue((ErrorEntity)result, result.GetRawResponse());
        }

        public virtual ClientResult Update(string errorId, BinaryContent content, RequestOptions options = null)
        {
            Argument.AssertNotNullOrEmpty(errorId, nameof(errorId));
            Argument.AssertNotNull(content, nameof(content));

            using PipelineMessage message = CreateUpdateRequest(errorId, content, options);
            return ClientResult.FromResponse(Pipeline.ProcessMessage(message, options));
        }

        public virtual async Task<ClientResult> UpdateAsync(string errorId, BinaryContent content, RequestOptions options = null)
        {
            Argument.AssertNotNullOrEmpty(errorId, nameof(errorId));
            Argument.AssertNotNull(content, nameof(content));

            using PipelineMessage message = CreateUpdateRequest(errorId, content, options);
            return ClientResult.FromResponse(await Pipeline.ProcessMessageAsync(message, options).ConfigureAwait(false));
        }

        public virtual ClientResult GetStats(string serviceName, DateTimeOffset? startTime, DateTimeOffset? endTime, RequestOptions options)
        {
            using PipelineMessage message = CreateGetStatsRequest(serviceName, startTime, endTime, options);
            return ClientResult.FromResponse(Pipeline.ProcessMessage(message, options));
        }

        public virtual async Task<ClientResult> GetStatsAsync(string serviceName, DateTimeOffset? startTime, DateTimeOffset? endTime, RequestOptions options)
        {
            using PipelineMessage message = CreateGetStatsRequest(serviceName, startTime, endTime, options);
            return ClientResult.FromResponse(await Pipeline.ProcessMessageAsync(message, options).ConfigureAwait(false));
        }

        public virtual ClientResult<ErrorStats> GetStats(string serviceName = default, DateTimeOffset? startTime = default, DateTimeOffset? endTime = default, CancellationToken cancellationToken = default)
        {
            ClientResult result = GetStats(serviceName, startTime, endTime, cancellationToken.ToRequestOptions());
            return ClientResult.FromValue((ErrorStats)result, result.GetRawResponse());
        }

        public virtual async Task<ClientResult<ErrorStats>> GetStatsAsync(string serviceName = default, DateTimeOffset? startTime = default, DateTimeOffset? endTime = default, CancellationToken cancellationToken = default)
        {
            ClientResult result = await GetStatsAsync(serviceName, startTime, endTime, cancellationToken.ToRequestOptions()).ConfigureAwait(false);
            return ClientResult.FromValue((ErrorStats)result, result.GetRawResponse());
        }

        public virtual ClientResult GetCorrelations(string errorId, RequestOptions options)
        {
            Argument.AssertNotNullOrEmpty(errorId, nameof(errorId));

            using PipelineMessage message = CreateGetCorrelationsRequest(errorId, options);
            return ClientResult.FromResponse(Pipeline.ProcessMessage(message, options));
        }

        public virtual async Task<ClientResult> GetCorrelationsAsync(string errorId, RequestOptions options)
        {
            Argument.AssertNotNullOrEmpty(errorId, nameof(errorId));

            using PipelineMessage message = CreateGetCorrelationsRequest(errorId, options);
            return ClientResult.FromResponse(await Pipeline.ProcessMessageAsync(message, options).ConfigureAwait(false));
        }

        public virtual ClientResult<ErrorCorrelation> GetCorrelations(string errorId, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNullOrEmpty(errorId, nameof(errorId));

            ClientResult result = GetCorrelations(errorId, cancellationToken.ToRequestOptions());
            return ClientResult.FromValue((ErrorCorrelation)result, result.GetRawResponse());
        }

        public virtual async Task<ClientResult<ErrorCorrelation>> GetCorrelationsAsync(string errorId, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNullOrEmpty(errorId, nameof(errorId));

            ClientResult result = await GetCorrelationsAsync(errorId, cancellationToken.ToRequestOptions()).ConfigureAwait(false);
            return ClientResult.FromValue((ErrorCorrelation)result, result.GetRawResponse());
        }
    }
}
