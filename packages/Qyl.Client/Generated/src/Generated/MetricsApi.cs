
#nullable disable

using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Threading;
using System.Threading.Tasks;
using Qyl.Client;
using Qyl.Common.Pagination;

namespace Qyl.Api
{
    public partial class MetricsApi
    {
        private readonly Uri _endpoint;

        protected MetricsApi()
        {
        }

        internal MetricsApi(ClientPipeline pipeline, Uri endpoint)
        {
            _endpoint = endpoint;
            Pipeline = pipeline;
        }

        public ClientPipeline Pipeline { get; }

        public virtual ClientResult GetAll(string serviceName, string namePattern, int? limit, string cursor, RequestOptions options)
        {
            using PipelineMessage message = CreateGetAllRequest(serviceName, namePattern, limit, cursor, options);
            return ClientResult.FromResponse(Pipeline.ProcessMessage(message, options));
        }

        public virtual async Task<ClientResult> GetAllAsync(string serviceName, string namePattern, int? limit, string cursor, RequestOptions options)
        {
            using PipelineMessage message = CreateGetAllRequest(serviceName, namePattern, limit, cursor, options);
            return ClientResult.FromResponse(await Pipeline.ProcessMessageAsync(message, options).ConfigureAwait(false));
        }

        public virtual ClientResult<CursorPageMetricMetadata> GetAll(string serviceName = default, string namePattern = default, int? limit = default, string cursor = default, CancellationToken cancellationToken = default)
        {
            ClientResult result = GetAll(serviceName, namePattern, limit, cursor, cancellationToken.ToRequestOptions());
            return ClientResult.FromValue((CursorPageMetricMetadata)result, result.GetRawResponse());
        }

        public virtual async Task<ClientResult<CursorPageMetricMetadata>> GetAllAsync(string serviceName = default, string namePattern = default, int? limit = default, string cursor = default, CancellationToken cancellationToken = default)
        {
            ClientResult result = await GetAllAsync(serviceName, namePattern, limit, cursor, cancellationToken.ToRequestOptions()).ConfigureAwait(false);
            return ClientResult.FromValue((CursorPageMetricMetadata)result, result.GetRawResponse());
        }

        public virtual ClientResult Query(BinaryContent content, RequestOptions options = null)
        {
            Argument.AssertNotNull(content, nameof(content));

            using PipelineMessage message = CreateQueryRequest(content, options);
            return ClientResult.FromResponse(Pipeline.ProcessMessage(message, options));
        }

        public virtual async Task<ClientResult> QueryAsync(BinaryContent content, RequestOptions options = null)
        {
            Argument.AssertNotNull(content, nameof(content));

            using PipelineMessage message = CreateQueryRequest(content, options);
            return ClientResult.FromResponse(await Pipeline.ProcessMessageAsync(message, options).ConfigureAwait(false));
        }

        public virtual ClientResult<MetricQueryResponse> Query(MetricQueryRequest request, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNull(request, nameof(request));

            ClientResult result = Query(request, cancellationToken.ToRequestOptions());
            return ClientResult.FromValue((MetricQueryResponse)result, result.GetRawResponse());
        }

        public virtual async Task<ClientResult<MetricQueryResponse>> QueryAsync(MetricQueryRequest request, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNull(request, nameof(request));

            ClientResult result = await QueryAsync(request, cancellationToken.ToRequestOptions()).ConfigureAwait(false);
            return ClientResult.FromValue((MetricQueryResponse)result, result.GetRawResponse());
        }

        public virtual ClientResult GetMetadata(string metricName, RequestOptions options)
        {
            Argument.AssertNotNullOrEmpty(metricName, nameof(metricName));

            using PipelineMessage message = CreateGetMetadataRequest(metricName, options);
            return ClientResult.FromResponse(Pipeline.ProcessMessage(message, options));
        }

        public virtual async Task<ClientResult> GetMetadataAsync(string metricName, RequestOptions options)
        {
            Argument.AssertNotNullOrEmpty(metricName, nameof(metricName));

            using PipelineMessage message = CreateGetMetadataRequest(metricName, options);
            return ClientResult.FromResponse(await Pipeline.ProcessMessageAsync(message, options).ConfigureAwait(false));
        }

        public virtual ClientResult<MetricMetadata> GetMetadata(string metricName, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNullOrEmpty(metricName, nameof(metricName));

            ClientResult result = GetMetadata(metricName, cancellationToken.ToRequestOptions());
            return ClientResult.FromValue((MetricMetadata)result, result.GetRawResponse());
        }

        public virtual async Task<ClientResult<MetricMetadata>> GetMetadataAsync(string metricName, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNullOrEmpty(metricName, nameof(metricName));

            ClientResult result = await GetMetadataAsync(metricName, cancellationToken.ToRequestOptions()).ConfigureAwait(false);
            return ClientResult.FromValue((MetricMetadata)result, result.GetRawResponse());
        }
    }
}
