
#nullable disable

using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Qyl.Client;
using Qyl.Common.Pagination;
using Qyl.Domains.Observe.Log;
using Qyl.OTel.Enums;
using Qyl.OTel.Logs;

namespace Qyl.Api
{
    public partial class LogsApi
    {
        private readonly Uri _endpoint;

        protected LogsApi()
        {
        }

        internal LogsApi(ClientPipeline pipeline, Uri endpoint)
        {
            _endpoint = endpoint;
            Pipeline = pipeline;
        }

        public ClientPipeline Pipeline { get; }

        public virtual ClientResult GetAll(string serviceName, int? severityMin, int? severityMax, string traceId, DateTimeOffset? startTime, DateTimeOffset? endTime, string query, int? limit, string cursor, string orderBy, RequestOptions options)
        {
            using PipelineMessage message = this.CreateGetAllRequest(serviceName, severityMin, severityMax, traceId, startTime, endTime, query, limit, cursor, orderBy, options);
            return ClientResult.FromResponse(Pipeline.ProcessMessage(message, options));
        }

        public virtual async Task<ClientResult> GetAllAsync(string serviceName, int? severityMin, int? severityMax, string traceId, DateTimeOffset? startTime, DateTimeOffset? endTime, string query, int? limit, string cursor, string orderBy, RequestOptions options)
        {
            using PipelineMessage message = this.CreateGetAllRequest(serviceName, severityMin, severityMax, traceId, startTime, endTime, query, limit, cursor, orderBy, options);
            return ClientResult.FromResponse(await Pipeline.ProcessMessageAsync(message, options).ConfigureAwait(false));
        }

        public virtual ClientResult<CursorPageLogRecord> GetAll(string serviceName = default, SeverityNumber? severityMin = default, SeverityNumber? severityMax = default, string traceId = default, DateTimeOffset? startTime = default, DateTimeOffset? endTime = default, string query = default, int? limit = default, string cursor = default, LogOrderBy? orderBy = default, CancellationToken cancellationToken = default)
        {
            ClientResult result = this.GetAll(serviceName, (int?)severityMin, (int?)severityMax, traceId, startTime, endTime, query, limit, cursor, orderBy?.ToSerialString(), cancellationToken.ToRequestOptions());
            return ClientResult.FromValue((CursorPageLogRecord)result, result.GetRawResponse());
        }

        public virtual async Task<ClientResult<CursorPageLogRecord>> GetAllAsync(string serviceName = default, SeverityNumber? severityMin = default, SeverityNumber? severityMax = default, string traceId = default, DateTimeOffset? startTime = default, DateTimeOffset? endTime = default, string query = default, int? limit = default, string cursor = default, LogOrderBy? orderBy = default, CancellationToken cancellationToken = default)
        {
            ClientResult result = await this.GetAllAsync(serviceName, (int?)severityMin, (int?)severityMax, traceId, startTime, endTime, query, limit, cursor, orderBy?.ToSerialString(), cancellationToken.ToRequestOptions()).ConfigureAwait(false);
            return ClientResult.FromValue((CursorPageLogRecord)result, result.GetRawResponse());
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

        public virtual ClientResult<CursorPageLogRecord> Search(LogQuery query, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNull(query, nameof(query));

            ClientResult result = Search(query, cancellationToken.ToRequestOptions());
            return ClientResult.FromValue((CursorPageLogRecord)result, result.GetRawResponse());
        }

        public virtual async Task<ClientResult<CursorPageLogRecord>> SearchAsync(LogQuery query, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNull(query, nameof(query));

            ClientResult result = await SearchAsync(query, cancellationToken.ToRequestOptions()).ConfigureAwait(false);
            return ClientResult.FromValue((CursorPageLogRecord)result, result.GetRawResponse());
        }

        public virtual ClientResult Aggregate(BinaryContent content, RequestOptions options = null)
        {
            Argument.AssertNotNull(content, nameof(content));

            using PipelineMessage message = CreateAggregateRequest(content, options);
            return ClientResult.FromResponse(Pipeline.ProcessMessage(message, options));
        }

        public virtual async Task<ClientResult> AggregateAsync(BinaryContent content, RequestOptions options = null)
        {
            Argument.AssertNotNull(content, nameof(content));

            using PipelineMessage message = CreateAggregateRequest(content, options);
            return ClientResult.FromResponse(await Pipeline.ProcessMessageAsync(message, options).ConfigureAwait(false));
        }

        public virtual ClientResult<LogAggregationResponse> Aggregate(LogAggregationRequest request, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNull(request, nameof(request));

            ClientResult result = Aggregate(request, cancellationToken.ToRequestOptions());
            return ClientResult.FromValue((LogAggregationResponse)result, result.GetRawResponse());
        }

        public virtual async Task<ClientResult<LogAggregationResponse>> AggregateAsync(LogAggregationRequest request, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNull(request, nameof(request));

            ClientResult result = await AggregateAsync(request, cancellationToken.ToRequestOptions()).ConfigureAwait(false);
            return ClientResult.FromValue((LogAggregationResponse)result, result.GetRawResponse());
        }

        public virtual ClientResult GetPatterns(string serviceName, DateTimeOffset? startTime, DateTimeOffset? endTime, int? minCount, RequestOptions options)
        {
            using PipelineMessage message = CreateGetPatternsRequest(serviceName, startTime, endTime, minCount, options);
            return ClientResult.FromResponse(Pipeline.ProcessMessage(message, options));
        }

        public virtual async Task<ClientResult> GetPatternsAsync(string serviceName, DateTimeOffset? startTime, DateTimeOffset? endTime, int? minCount, RequestOptions options)
        {
            using PipelineMessage message = CreateGetPatternsRequest(serviceName, startTime, endTime, minCount, options);
            return ClientResult.FromResponse(await Pipeline.ProcessMessageAsync(message, options).ConfigureAwait(false));
        }

        public virtual ClientResult<IReadOnlyList<LogPattern>> GetPatterns(string serviceName = default, DateTimeOffset? startTime = default, DateTimeOffset? endTime = default, int? minCount = default, CancellationToken cancellationToken = default)
        {
            ClientResult result = GetPatterns(serviceName, startTime, endTime, minCount, cancellationToken.ToRequestOptions());
            List<LogPattern> value = new List<LogPattern>();
            BinaryData data = result.GetRawResponse().Content;
            using JsonDocument document = JsonDocument.Parse(data);
            foreach (var item in document.RootElement.EnumerateArray())
            {
                value.Add(LogPattern.DeserializeLogPattern(item, ModelSerializationExtensions.WireOptions));
            }
            return ClientResult.FromValue((IReadOnlyList<LogPattern>)value, result.GetRawResponse());
        }

        public virtual async Task<ClientResult<IReadOnlyList<LogPattern>>> GetPatternsAsync(string serviceName = default, DateTimeOffset? startTime = default, DateTimeOffset? endTime = default, int? minCount = default, CancellationToken cancellationToken = default)
        {
            ClientResult result = await GetPatternsAsync(serviceName, startTime, endTime, minCount, cancellationToken.ToRequestOptions()).ConfigureAwait(false);
            List<LogPattern> value = new List<LogPattern>();
            BinaryData data = result.GetRawResponse().Content;
            using JsonDocument document = JsonDocument.Parse(data);
            foreach (var item in document.RootElement.EnumerateArray())
            {
                value.Add(LogPattern.DeserializeLogPattern(item, ModelSerializationExtensions.WireOptions));
            }
            return ClientResult.FromValue((IReadOnlyList<LogPattern>)value, result.GetRawResponse());
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

        public virtual ClientResult<LogStats> GetStats(string serviceName = default, DateTimeOffset? startTime = default, DateTimeOffset? endTime = default, CancellationToken cancellationToken = default)
        {
            ClientResult result = GetStats(serviceName, startTime, endTime, cancellationToken.ToRequestOptions());
            return ClientResult.FromValue((LogStats)result, result.GetRawResponse());
        }

        public virtual async Task<ClientResult<LogStats>> GetStatsAsync(string serviceName = default, DateTimeOffset? startTime = default, DateTimeOffset? endTime = default, CancellationToken cancellationToken = default)
        {
            ClientResult result = await GetStatsAsync(serviceName, startTime, endTime, cancellationToken.ToRequestOptions()).ConfigureAwait(false);
            return ClientResult.FromValue((LogStats)result, result.GetRawResponse());
        }
    }
}
