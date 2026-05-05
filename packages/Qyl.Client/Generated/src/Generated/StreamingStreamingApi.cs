
#nullable disable

using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Qyl.Client;

namespace Qyl.Api._Streaming
{
    public partial class StreamingStreamingApi
    {
        private readonly Uri _endpoint;

        protected StreamingStreamingApi()
        {
        }

        internal StreamingStreamingApi(ClientPipeline pipeline, Uri endpoint)
        {
            _endpoint = endpoint;
            Pipeline = pipeline;
        }

        public ClientPipeline Pipeline { get; }

        public virtual ClientResult StreamEvents(IEnumerable<StreamEventType> types, string serviceName, double? sampleRate, RequestOptions options)
        {
            using PipelineMessage message = CreateStreamEventsRequest(types, serviceName, sampleRate, options);
            return ClientResult.FromResponse(Pipeline.ProcessMessage(message, options));
        }

        public virtual async Task<ClientResult> StreamEventsAsync(IEnumerable<StreamEventType> types, string serviceName, double? sampleRate, RequestOptions options)
        {
            using PipelineMessage message = CreateStreamEventsRequest(types, serviceName, sampleRate, options);
            return ClientResult.FromResponse(await Pipeline.ProcessMessageAsync(message, options).ConfigureAwait(false));
        }

        public virtual ClientResult<BinaryData> StreamEvents(IEnumerable<StreamEventType> types = default, string serviceName = default, double? sampleRate = default, CancellationToken cancellationToken = default)
        {
            ClientResult result = StreamEvents(types, serviceName, sampleRate, cancellationToken.ToRequestOptions());
            return ClientResult.FromValue(result.GetRawResponse().Content, result.GetRawResponse());
        }

        public virtual async Task<ClientResult<BinaryData>> StreamEventsAsync(IEnumerable<StreamEventType> types = default, string serviceName = default, double? sampleRate = default, CancellationToken cancellationToken = default)
        {
            ClientResult result = await StreamEventsAsync(types, serviceName, sampleRate, cancellationToken.ToRequestOptions()).ConfigureAwait(false);
            return ClientResult.FromValue(result.GetRawResponse().Content, result.GetRawResponse());
        }

        public virtual ClientResult StreamTraces(string serviceName, long? minDurationMs, RequestOptions options)
        {
            using PipelineMessage message = CreateStreamTracesRequest(serviceName, minDurationMs, options);
            return ClientResult.FromResponse(Pipeline.ProcessMessage(message, options));
        }

        public virtual async Task<ClientResult> StreamTracesAsync(string serviceName, long? minDurationMs, RequestOptions options)
        {
            using PipelineMessage message = CreateStreamTracesRequest(serviceName, minDurationMs, options);
            return ClientResult.FromResponse(await Pipeline.ProcessMessageAsync(message, options).ConfigureAwait(false));
        }

        public virtual ClientResult<BinaryData> StreamTraces(string serviceName = default, long? minDurationMs = default, CancellationToken cancellationToken = default)
        {
            ClientResult result = StreamTraces(serviceName, minDurationMs, cancellationToken.ToRequestOptions());
            return ClientResult.FromValue(result.GetRawResponse().Content, result.GetRawResponse());
        }

        public virtual async Task<ClientResult<BinaryData>> StreamTracesAsync(string serviceName = default, long? minDurationMs = default, CancellationToken cancellationToken = default)
        {
            ClientResult result = await StreamTracesAsync(serviceName, minDurationMs, cancellationToken.ToRequestOptions()).ConfigureAwait(false);
            return ClientResult.FromValue(result.GetRawResponse().Content, result.GetRawResponse());
        }

        public virtual ClientResult StreamTraceSpans(string traceId, RequestOptions options)
        {
            Argument.AssertNotNullOrEmpty(traceId, nameof(traceId));

            using PipelineMessage message = CreateStreamTraceSpansRequest(traceId, options);
            return ClientResult.FromResponse(Pipeline.ProcessMessage(message, options));
        }

        public virtual async Task<ClientResult> StreamTraceSpansAsync(string traceId, RequestOptions options)
        {
            Argument.AssertNotNullOrEmpty(traceId, nameof(traceId));

            using PipelineMessage message = CreateStreamTraceSpansRequest(traceId, options);
            return ClientResult.FromResponse(await Pipeline.ProcessMessageAsync(message, options).ConfigureAwait(false));
        }

        public virtual ClientResult<BinaryData> StreamTraceSpans(string traceId, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNullOrEmpty(traceId, nameof(traceId));

            ClientResult result = StreamTraceSpans(traceId, cancellationToken.ToRequestOptions());
            return ClientResult.FromValue(result.GetRawResponse().Content, result.GetRawResponse());
        }

        public virtual async Task<ClientResult<BinaryData>> StreamTraceSpansAsync(string traceId, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNullOrEmpty(traceId, nameof(traceId));

            ClientResult result = await StreamTraceSpansAsync(traceId, cancellationToken.ToRequestOptions()).ConfigureAwait(false);
            return ClientResult.FromValue(result.GetRawResponse().Content, result.GetRawResponse());
        }

        public virtual ClientResult StreamLogs(string serviceName, int? minSeverity, string query, RequestOptions options)
        {
            using PipelineMessage message = CreateStreamLogsRequest(serviceName, minSeverity, query, options);
            return ClientResult.FromResponse(Pipeline.ProcessMessage(message, options));
        }

        public virtual async Task<ClientResult> StreamLogsAsync(string serviceName, int? minSeverity, string query, RequestOptions options)
        {
            using PipelineMessage message = CreateStreamLogsRequest(serviceName, minSeverity, query, options);
            return ClientResult.FromResponse(await Pipeline.ProcessMessageAsync(message, options).ConfigureAwait(false));
        }

        public virtual ClientResult<BinaryData> StreamLogs(string serviceName = default, int? minSeverity = default, string query = default, CancellationToken cancellationToken = default)
        {
            ClientResult result = StreamLogs(serviceName, minSeverity, query, cancellationToken.ToRequestOptions());
            return ClientResult.FromValue(result.GetRawResponse().Content, result.GetRawResponse());
        }

        public virtual async Task<ClientResult<BinaryData>> StreamLogsAsync(string serviceName = default, int? minSeverity = default, string query = default, CancellationToken cancellationToken = default)
        {
            ClientResult result = await StreamLogsAsync(serviceName, minSeverity, query, cancellationToken.ToRequestOptions()).ConfigureAwait(false);
            return ClientResult.FromValue(result.GetRawResponse().Content, result.GetRawResponse());
        }

        public virtual ClientResult StreamMetrics(string metricName, string serviceName, RequestOptions options)
        {
            using PipelineMessage message = CreateStreamMetricsRequest(metricName, serviceName, options);
            return ClientResult.FromResponse(Pipeline.ProcessMessage(message, options));
        }

        public virtual async Task<ClientResult> StreamMetricsAsync(string metricName, string serviceName, RequestOptions options)
        {
            using PipelineMessage message = CreateStreamMetricsRequest(metricName, serviceName, options);
            return ClientResult.FromResponse(await Pipeline.ProcessMessageAsync(message, options).ConfigureAwait(false));
        }

        public virtual ClientResult<BinaryData> StreamMetrics(string metricName = default, string serviceName = default, CancellationToken cancellationToken = default)
        {
            ClientResult result = StreamMetrics(metricName, serviceName, cancellationToken.ToRequestOptions());
            return ClientResult.FromValue(result.GetRawResponse().Content, result.GetRawResponse());
        }

        public virtual async Task<ClientResult<BinaryData>> StreamMetricsAsync(string metricName = default, string serviceName = default, CancellationToken cancellationToken = default)
        {
            ClientResult result = await StreamMetricsAsync(metricName, serviceName, cancellationToken.ToRequestOptions()).ConfigureAwait(false);
            return ClientResult.FromValue(result.GetRawResponse().Content, result.GetRawResponse());
        }

        public virtual ClientResult StreamDeployments(string serviceName, string environment, RequestOptions options)
        {
            using PipelineMessage message = CreateStreamDeploymentsRequest(serviceName, environment, options);
            return ClientResult.FromResponse(Pipeline.ProcessMessage(message, options));
        }

        public virtual async Task<ClientResult> StreamDeploymentsAsync(string serviceName, string environment, RequestOptions options)
        {
            using PipelineMessage message = CreateStreamDeploymentsRequest(serviceName, environment, options);
            return ClientResult.FromResponse(await Pipeline.ProcessMessageAsync(message, options).ConfigureAwait(false));
        }

        public virtual ClientResult<BinaryData> StreamDeployments(string serviceName = default, string environment = default, CancellationToken cancellationToken = default)
        {
            ClientResult result = StreamDeployments(serviceName, environment, cancellationToken.ToRequestOptions());
            return ClientResult.FromValue(result.GetRawResponse().Content, result.GetRawResponse());
        }

        public virtual async Task<ClientResult<BinaryData>> StreamDeploymentsAsync(string serviceName = default, string environment = default, CancellationToken cancellationToken = default)
        {
            ClientResult result = await StreamDeploymentsAsync(serviceName, environment, cancellationToken.ToRequestOptions()).ConfigureAwait(false);
            return ClientResult.FromValue(result.GetRawResponse().Content, result.GetRawResponse());
        }
    }
}
