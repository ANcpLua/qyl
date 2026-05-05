
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
using Qyl.Domains.Issues;

namespace Qyl.Api
{
    public partial class IssuesApi
    {
        private readonly Uri _endpoint;

        protected IssuesApi()
        {
        }

        internal IssuesApi(ClientPipeline pipeline, Uri endpoint)
        {
            _endpoint = endpoint;
            Pipeline = pipeline;
        }

        public ClientPipeline Pipeline { get; }

        public virtual ClientResult GetAll(string projectId, string status, string priority, string level, DateTimeOffset? startTime, DateTimeOffset? endTime, int? limit, string cursor, RequestOptions options)
        {
            using PipelineMessage message = CreateGetAllRequest(projectId, status, priority, level, startTime, endTime, limit, cursor, options);
            return ClientResult.FromResponse(Pipeline.ProcessMessage(message, options));
        }

        public virtual async Task<ClientResult> GetAllAsync(string projectId, string status, string priority, string level, DateTimeOffset? startTime, DateTimeOffset? endTime, int? limit, string cursor, RequestOptions options)
        {
            using PipelineMessage message = CreateGetAllRequest(projectId, status, priority, level, startTime, endTime, limit, cursor, options);
            return ClientResult.FromResponse(await Pipeline.ProcessMessageAsync(message, options).ConfigureAwait(false));
        }

        public virtual ClientResult<CursorPageErrorIssueEntity> GetAll(string projectId = default, IssueStatus? status = default, IssuePriority? priority = default, IssueLevel? level = default, DateTimeOffset? startTime = default, DateTimeOffset? endTime = default, int? limit = default, string cursor = default, CancellationToken cancellationToken = default)
        {
            ClientResult result = GetAll(projectId, status?.ToSerialString(), priority?.ToSerialString(), level?.ToSerialString(), startTime, endTime, limit, cursor, cancellationToken.ToRequestOptions());
            return ClientResult.FromValue((CursorPageErrorIssueEntity)result, result.GetRawResponse());
        }

        public virtual async Task<ClientResult<CursorPageErrorIssueEntity>> GetAllAsync(string projectId = default, IssueStatus? status = default, IssuePriority? priority = default, IssueLevel? level = default, DateTimeOffset? startTime = default, DateTimeOffset? endTime = default, int? limit = default, string cursor = default, CancellationToken cancellationToken = default)
        {
            ClientResult result = await GetAllAsync(projectId, status?.ToSerialString(), priority?.ToSerialString(), level?.ToSerialString(), startTime, endTime, limit, cursor, cancellationToken.ToRequestOptions()).ConfigureAwait(false);
            return ClientResult.FromValue((CursorPageErrorIssueEntity)result, result.GetRawResponse());
        }

        public virtual ClientResult Get(string issueId, RequestOptions options)
        {
            Argument.AssertNotNullOrEmpty(issueId, nameof(issueId));

            using PipelineMessage message = CreateGetRequest(issueId, options);
            return ClientResult.FromResponse(Pipeline.ProcessMessage(message, options));
        }

        public virtual async Task<ClientResult> GetAsync(string issueId, RequestOptions options)
        {
            Argument.AssertNotNullOrEmpty(issueId, nameof(issueId));

            using PipelineMessage message = CreateGetRequest(issueId, options);
            return ClientResult.FromResponse(await Pipeline.ProcessMessageAsync(message, options).ConfigureAwait(false));
        }

        public virtual ClientResult<ErrorIssueEntity> Get(string issueId, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNullOrEmpty(issueId, nameof(issueId));

            ClientResult result = Get(issueId, cancellationToken.ToRequestOptions());
            return ClientResult.FromValue((ErrorIssueEntity)result, result.GetRawResponse());
        }

        public virtual async Task<ClientResult<ErrorIssueEntity>> GetAsync(string issueId, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNullOrEmpty(issueId, nameof(issueId));

            ClientResult result = await GetAsync(issueId, cancellationToken.ToRequestOptions()).ConfigureAwait(false);
            return ClientResult.FromValue((ErrorIssueEntity)result, result.GetRawResponse());
        }

        public virtual ClientResult Update(string issueId, BinaryContent content, RequestOptions options = null)
        {
            Argument.AssertNotNullOrEmpty(issueId, nameof(issueId));
            Argument.AssertNotNull(content, nameof(content));

            using PipelineMessage message = CreateUpdateRequest(issueId, content, options);
            return ClientResult.FromResponse(Pipeline.ProcessMessage(message, options));
        }

        public virtual async Task<ClientResult> UpdateAsync(string issueId, BinaryContent content, RequestOptions options = null)
        {
            Argument.AssertNotNullOrEmpty(issueId, nameof(issueId));
            Argument.AssertNotNull(content, nameof(content));

            using PipelineMessage message = CreateUpdateRequest(issueId, content, options);
            return ClientResult.FromResponse(await Pipeline.ProcessMessageAsync(message, options).ConfigureAwait(false));
        }

        public virtual ClientResult GetEvents(string issueId, int? limit, string cursor, RequestOptions options)
        {
            Argument.AssertNotNullOrEmpty(issueId, nameof(issueId));

            using PipelineMessage message = CreateGetEventsRequest(issueId, limit, cursor, options);
            return ClientResult.FromResponse(Pipeline.ProcessMessage(message, options));
        }

        public virtual async Task<ClientResult> GetEventsAsync(string issueId, int? limit, string cursor, RequestOptions options)
        {
            Argument.AssertNotNullOrEmpty(issueId, nameof(issueId));

            using PipelineMessage message = CreateGetEventsRequest(issueId, limit, cursor, options);
            return ClientResult.FromResponse(await Pipeline.ProcessMessageAsync(message, options).ConfigureAwait(false));
        }

        public virtual ClientResult<CursorPageErrorIssueEventEntity> GetEvents(string issueId, int? limit = default, string cursor = default, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNullOrEmpty(issueId, nameof(issueId));

            ClientResult result = GetEvents(issueId, limit, cursor, cancellationToken.ToRequestOptions());
            return ClientResult.FromValue((CursorPageErrorIssueEventEntity)result, result.GetRawResponse());
        }

        public virtual async Task<ClientResult<CursorPageErrorIssueEventEntity>> GetEventsAsync(string issueId, int? limit = default, string cursor = default, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNullOrEmpty(issueId, nameof(issueId));

            ClientResult result = await GetEventsAsync(issueId, limit, cursor, cancellationToken.ToRequestOptions()).ConfigureAwait(false);
            return ClientResult.FromValue((CursorPageErrorIssueEventEntity)result, result.GetRawResponse());
        }

        public virtual ClientResult GetBreadcrumbs(string issueId, int? limit, RequestOptions options)
        {
            Argument.AssertNotNullOrEmpty(issueId, nameof(issueId));

            using PipelineMessage message = CreateGetBreadcrumbsRequest(issueId, limit, options);
            return ClientResult.FromResponse(Pipeline.ProcessMessage(message, options));
        }

        public virtual async Task<ClientResult> GetBreadcrumbsAsync(string issueId, int? limit, RequestOptions options)
        {
            Argument.AssertNotNullOrEmpty(issueId, nameof(issueId));

            using PipelineMessage message = CreateGetBreadcrumbsRequest(issueId, limit, options);
            return ClientResult.FromResponse(await Pipeline.ProcessMessageAsync(message, options).ConfigureAwait(false));
        }

        public virtual ClientResult<IReadOnlyList<ErrorBreadcrumbEntity>> GetBreadcrumbs(string issueId, int? limit = default, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNullOrEmpty(issueId, nameof(issueId));

            ClientResult result = GetBreadcrumbs(issueId, limit, cancellationToken.ToRequestOptions());
            List<ErrorBreadcrumbEntity> value = new List<ErrorBreadcrumbEntity>();
            BinaryData data = result.GetRawResponse().Content;
            using JsonDocument document = JsonDocument.Parse(data);
            foreach (var item in document.RootElement.EnumerateArray())
            {
                value.Add(ErrorBreadcrumbEntity.DeserializeErrorBreadcrumbEntity(item, ModelSerializationExtensions.WireOptions));
            }
            return ClientResult.FromValue((IReadOnlyList<ErrorBreadcrumbEntity>)value, result.GetRawResponse());
        }

        public virtual async Task<ClientResult<IReadOnlyList<ErrorBreadcrumbEntity>>> GetBreadcrumbsAsync(string issueId, int? limit = default, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNullOrEmpty(issueId, nameof(issueId));

            ClientResult result = await GetBreadcrumbsAsync(issueId, limit, cancellationToken.ToRequestOptions()).ConfigureAwait(false);
            List<ErrorBreadcrumbEntity> value = new List<ErrorBreadcrumbEntity>();
            BinaryData data = result.GetRawResponse().Content;
            using JsonDocument document = JsonDocument.Parse(data);
            foreach (var item in document.RootElement.EnumerateArray())
            {
                value.Add(ErrorBreadcrumbEntity.DeserializeErrorBreadcrumbEntity(item, ModelSerializationExtensions.WireOptions));
            }
            return ClientResult.FromValue((IReadOnlyList<ErrorBreadcrumbEntity>)value, result.GetRawResponse());
        }
    }
}
