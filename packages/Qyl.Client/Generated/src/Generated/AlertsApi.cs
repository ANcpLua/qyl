
#nullable disable

using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Threading;
using System.Threading.Tasks;
using Qyl.Client;
using Qyl.Common.Pagination;
using Qyl.Domains.Alerting;

namespace Qyl.Api
{
    public partial class AlertsApi
    {
        private readonly Uri _endpoint;

        protected AlertsApi()
        {
        }

        internal AlertsApi(ClientPipeline pipeline, Uri endpoint)
        {
            _endpoint = endpoint;
            Pipeline = pipeline;
        }

        public ClientPipeline Pipeline { get; }

        public virtual ClientResult GetRules(string projectId, bool? enabled, int? limit, string cursor, RequestOptions options)
        {
            using PipelineMessage message = CreateGetRulesRequest(projectId, enabled, limit, cursor, options);
            return ClientResult.FromResponse(Pipeline.ProcessMessage(message, options));
        }

        public virtual async Task<ClientResult> GetRulesAsync(string projectId, bool? enabled, int? limit, string cursor, RequestOptions options)
        {
            using PipelineMessage message = CreateGetRulesRequest(projectId, enabled, limit, cursor, options);
            return ClientResult.FromResponse(await Pipeline.ProcessMessageAsync(message, options).ConfigureAwait(false));
        }

        public virtual ClientResult<CursorPageAlertRuleEntity> GetRules(string projectId = default, bool? enabled = default, int? limit = default, string cursor = default, CancellationToken cancellationToken = default)
        {
            ClientResult result = GetRules(projectId, enabled, limit, cursor, cancellationToken.ToRequestOptions());
            return ClientResult.FromValue((CursorPageAlertRuleEntity)result, result.GetRawResponse());
        }

        public virtual async Task<ClientResult<CursorPageAlertRuleEntity>> GetRulesAsync(string projectId = default, bool? enabled = default, int? limit = default, string cursor = default, CancellationToken cancellationToken = default)
        {
            ClientResult result = await GetRulesAsync(projectId, enabled, limit, cursor, cancellationToken.ToRequestOptions()).ConfigureAwait(false);
            return ClientResult.FromValue((CursorPageAlertRuleEntity)result, result.GetRawResponse());
        }

        public virtual ClientResult GetRule(string ruleId, RequestOptions options)
        {
            Argument.AssertNotNullOrEmpty(ruleId, nameof(ruleId));

            using PipelineMessage message = CreateGetRuleRequest(ruleId, options);
            return ClientResult.FromResponse(Pipeline.ProcessMessage(message, options));
        }

        public virtual async Task<ClientResult> GetRuleAsync(string ruleId, RequestOptions options)
        {
            Argument.AssertNotNullOrEmpty(ruleId, nameof(ruleId));

            using PipelineMessage message = CreateGetRuleRequest(ruleId, options);
            return ClientResult.FromResponse(await Pipeline.ProcessMessageAsync(message, options).ConfigureAwait(false));
        }

        public virtual ClientResult<AlertRuleEntity> GetRule(string ruleId, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNullOrEmpty(ruleId, nameof(ruleId));

            ClientResult result = GetRule(ruleId, cancellationToken.ToRequestOptions());
            return ClientResult.FromValue((AlertRuleEntity)result, result.GetRawResponse());
        }

        public virtual async Task<ClientResult<AlertRuleEntity>> GetRuleAsync(string ruleId, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNullOrEmpty(ruleId, nameof(ruleId));

            ClientResult result = await GetRuleAsync(ruleId, cancellationToken.ToRequestOptions()).ConfigureAwait(false);
            return ClientResult.FromValue((AlertRuleEntity)result, result.GetRawResponse());
        }

        public virtual ClientResult CreateRule(BinaryContent content, RequestOptions options = null)
        {
            Argument.AssertNotNull(content, nameof(content));

            using PipelineMessage message = CreateCreateRuleRequest(content, options);
            return ClientResult.FromResponse(Pipeline.ProcessMessage(message, options));
        }

        public virtual async Task<ClientResult> CreateRuleAsync(BinaryContent content, RequestOptions options = null)
        {
            Argument.AssertNotNull(content, nameof(content));

            using PipelineMessage message = CreateCreateRuleRequest(content, options);
            return ClientResult.FromResponse(await Pipeline.ProcessMessageAsync(message, options).ConfigureAwait(false));
        }

        public virtual ClientResult<AlertRuleEntity> CreateRule(AlertRuleEntity rule, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNull(rule, nameof(rule));

            ClientResult result = CreateRule(rule, cancellationToken.ToRequestOptions());
            return ClientResult.FromValue((AlertRuleEntity)result, result.GetRawResponse());
        }

        public virtual async Task<ClientResult<AlertRuleEntity>> CreateRuleAsync(AlertRuleEntity rule, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNull(rule, nameof(rule));

            ClientResult result = await CreateRuleAsync(rule, cancellationToken.ToRequestOptions()).ConfigureAwait(false);
            return ClientResult.FromValue((AlertRuleEntity)result, result.GetRawResponse());
        }

        public virtual ClientResult UpdateRule(string ruleId, BinaryContent content, RequestOptions options = null)
        {
            Argument.AssertNotNullOrEmpty(ruleId, nameof(ruleId));
            Argument.AssertNotNull(content, nameof(content));

            using PipelineMessage message = CreateUpdateRuleRequest(ruleId, content, options);
            return ClientResult.FromResponse(Pipeline.ProcessMessage(message, options));
        }

        public virtual async Task<ClientResult> UpdateRuleAsync(string ruleId, BinaryContent content, RequestOptions options = null)
        {
            Argument.AssertNotNullOrEmpty(ruleId, nameof(ruleId));
            Argument.AssertNotNull(content, nameof(content));

            using PipelineMessage message = CreateUpdateRuleRequest(ruleId, content, options);
            return ClientResult.FromResponse(await Pipeline.ProcessMessageAsync(message, options).ConfigureAwait(false));
        }

        public virtual ClientResult<AlertRuleEntity> UpdateRule(string ruleId, AlertRuleEntity rule, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNullOrEmpty(ruleId, nameof(ruleId));
            Argument.AssertNotNull(rule, nameof(rule));

            ClientResult result = UpdateRule(ruleId, rule, cancellationToken.ToRequestOptions());
            return ClientResult.FromValue((AlertRuleEntity)result, result.GetRawResponse());
        }

        public virtual async Task<ClientResult<AlertRuleEntity>> UpdateRuleAsync(string ruleId, AlertRuleEntity rule, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNullOrEmpty(ruleId, nameof(ruleId));
            Argument.AssertNotNull(rule, nameof(rule));

            ClientResult result = await UpdateRuleAsync(ruleId, rule, cancellationToken.ToRequestOptions()).ConfigureAwait(false);
            return ClientResult.FromValue((AlertRuleEntity)result, result.GetRawResponse());
        }

        public virtual ClientResult DeleteRule(string ruleId, RequestOptions options)
        {
            Argument.AssertNotNullOrEmpty(ruleId, nameof(ruleId));

            using PipelineMessage message = CreateDeleteRuleRequest(ruleId, options);
            return ClientResult.FromResponse(Pipeline.ProcessMessage(message, options));
        }

        public virtual async Task<ClientResult> DeleteRuleAsync(string ruleId, RequestOptions options)
        {
            Argument.AssertNotNullOrEmpty(ruleId, nameof(ruleId));

            using PipelineMessage message = CreateDeleteRuleRequest(ruleId, options);
            return ClientResult.FromResponse(await Pipeline.ProcessMessageAsync(message, options).ConfigureAwait(false));
        }

        public virtual ClientResult DeleteRule(string ruleId, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNullOrEmpty(ruleId, nameof(ruleId));

            return DeleteRule(ruleId, cancellationToken.ToRequestOptions());
        }

        public virtual async Task<ClientResult> DeleteRuleAsync(string ruleId, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNullOrEmpty(ruleId, nameof(ruleId));

            return await DeleteRuleAsync(ruleId, cancellationToken.ToRequestOptions()).ConfigureAwait(false);
        }

        public virtual ClientResult GetFirings(string ruleId, string status, DateTimeOffset? startTime, DateTimeOffset? endTime, int? limit, string cursor, RequestOptions options)
        {
            using PipelineMessage message = CreateGetFiringsRequest(ruleId, status, startTime, endTime, limit, cursor, options);
            return ClientResult.FromResponse(Pipeline.ProcessMessage(message, options));
        }

        public virtual async Task<ClientResult> GetFiringsAsync(string ruleId, string status, DateTimeOffset? startTime, DateTimeOffset? endTime, int? limit, string cursor, RequestOptions options)
        {
            using PipelineMessage message = CreateGetFiringsRequest(ruleId, status, startTime, endTime, limit, cursor, options);
            return ClientResult.FromResponse(await Pipeline.ProcessMessageAsync(message, options).ConfigureAwait(false));
        }

        public virtual ClientResult<CursorPageAlertFiringEntity> GetFirings(string ruleId = default, AlertFiringStatus? status = default, DateTimeOffset? startTime = default, DateTimeOffset? endTime = default, int? limit = default, string cursor = default, CancellationToken cancellationToken = default)
        {
            ClientResult result = GetFirings(ruleId, status?.ToSerialString(), startTime, endTime, limit, cursor, cancellationToken.ToRequestOptions());
            return ClientResult.FromValue((CursorPageAlertFiringEntity)result, result.GetRawResponse());
        }

        public virtual async Task<ClientResult<CursorPageAlertFiringEntity>> GetFiringsAsync(string ruleId = default, AlertFiringStatus? status = default, DateTimeOffset? startTime = default, DateTimeOffset? endTime = default, int? limit = default, string cursor = default, CancellationToken cancellationToken = default)
        {
            ClientResult result = await GetFiringsAsync(ruleId, status?.ToSerialString(), startTime, endTime, limit, cursor, cancellationToken.ToRequestOptions()).ConfigureAwait(false);
            return ClientResult.FromValue((CursorPageAlertFiringEntity)result, result.GetRawResponse());
        }

        public virtual ClientResult AcknowledgeFiring(string firingId, BinaryContent content, RequestOptions options = null)
        {
            Argument.AssertNotNullOrEmpty(firingId, nameof(firingId));
            Argument.AssertNotNull(content, nameof(content));

            using PipelineMessage message = CreateAcknowledgeFiringRequest(firingId, content, options);
            return ClientResult.FromResponse(Pipeline.ProcessMessage(message, options));
        }

        public virtual async Task<ClientResult> AcknowledgeFiringAsync(string firingId, BinaryContent content, RequestOptions options = null)
        {
            Argument.AssertNotNullOrEmpty(firingId, nameof(firingId));
            Argument.AssertNotNull(content, nameof(content));

            using PipelineMessage message = CreateAcknowledgeFiringRequest(firingId, content, options);
            return ClientResult.FromResponse(await Pipeline.ProcessMessageAsync(message, options).ConfigureAwait(false));
        }

        public virtual ClientResult<AlertFiringEntity> AcknowledgeFiring(string firingId, AlertFiringAcknowledgement acknowledgement, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNullOrEmpty(firingId, nameof(firingId));
            Argument.AssertNotNull(acknowledgement, nameof(acknowledgement));

            ClientResult result = AcknowledgeFiring(firingId, acknowledgement, cancellationToken.ToRequestOptions());
            return ClientResult.FromValue((AlertFiringEntity)result, result.GetRawResponse());
        }

        public virtual async Task<ClientResult<AlertFiringEntity>> AcknowledgeFiringAsync(string firingId, AlertFiringAcknowledgement acknowledgement, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNullOrEmpty(firingId, nameof(firingId));
            Argument.AssertNotNull(acknowledgement, nameof(acknowledgement));

            ClientResult result = await AcknowledgeFiringAsync(firingId, acknowledgement, cancellationToken.ToRequestOptions()).ConfigureAwait(false);
            return ClientResult.FromValue((AlertFiringEntity)result, result.GetRawResponse());
        }

        public virtual ClientResult ResolveFiring(string firingId, RequestOptions options)
        {
            Argument.AssertNotNullOrEmpty(firingId, nameof(firingId));

            using PipelineMessage message = CreateResolveFiringRequest(firingId, options);
            return ClientResult.FromResponse(Pipeline.ProcessMessage(message, options));
        }

        public virtual async Task<ClientResult> ResolveFiringAsync(string firingId, RequestOptions options)
        {
            Argument.AssertNotNullOrEmpty(firingId, nameof(firingId));

            using PipelineMessage message = CreateResolveFiringRequest(firingId, options);
            return ClientResult.FromResponse(await Pipeline.ProcessMessageAsync(message, options).ConfigureAwait(false));
        }

        public virtual ClientResult<AlertFiringEntity> ResolveFiring(string firingId, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNullOrEmpty(firingId, nameof(firingId));

            ClientResult result = ResolveFiring(firingId, cancellationToken.ToRequestOptions());
            return ClientResult.FromValue((AlertFiringEntity)result, result.GetRawResponse());
        }

        public virtual async Task<ClientResult<AlertFiringEntity>> ResolveFiringAsync(string firingId, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNullOrEmpty(firingId, nameof(firingId));

            ClientResult result = await ResolveFiringAsync(firingId, cancellationToken.ToRequestOptions()).ConfigureAwait(false);
            return ClientResult.FromValue((AlertFiringEntity)result, result.GetRawResponse());
        }

        public virtual ClientResult GetFixRuns(string issueId, string status, int? limit, string cursor, RequestOptions options)
        {
            using PipelineMessage message = CreateGetFixRunsRequest(issueId, status, limit, cursor, options);
            return ClientResult.FromResponse(Pipeline.ProcessMessage(message, options));
        }

        public virtual async Task<ClientResult> GetFixRunsAsync(string issueId, string status, int? limit, string cursor, RequestOptions options)
        {
            using PipelineMessage message = CreateGetFixRunsRequest(issueId, status, limit, cursor, options);
            return ClientResult.FromResponse(await Pipeline.ProcessMessageAsync(message, options).ConfigureAwait(false));
        }

        public virtual ClientResult<CursorPageFixRunEntity> GetFixRuns(string issueId = default, FixRunStatus? status = default, int? limit = default, string cursor = default, CancellationToken cancellationToken = default)
        {
            ClientResult result = GetFixRuns(issueId, status?.ToSerialString(), limit, cursor, cancellationToken.ToRequestOptions());
            return ClientResult.FromValue((CursorPageFixRunEntity)result, result.GetRawResponse());
        }

        public virtual async Task<ClientResult<CursorPageFixRunEntity>> GetFixRunsAsync(string issueId = default, FixRunStatus? status = default, int? limit = default, string cursor = default, CancellationToken cancellationToken = default)
        {
            ClientResult result = await GetFixRunsAsync(issueId, status?.ToSerialString(), limit, cursor, cancellationToken.ToRequestOptions()).ConfigureAwait(false);
            return ClientResult.FromValue((CursorPageFixRunEntity)result, result.GetRawResponse());
        }

        public virtual ClientResult GetFixRun(string fixId, RequestOptions options)
        {
            Argument.AssertNotNullOrEmpty(fixId, nameof(fixId));

            using PipelineMessage message = CreateGetFixRunRequest(fixId, options);
            return ClientResult.FromResponse(Pipeline.ProcessMessage(message, options));
        }

        public virtual async Task<ClientResult> GetFixRunAsync(string fixId, RequestOptions options)
        {
            Argument.AssertNotNullOrEmpty(fixId, nameof(fixId));

            using PipelineMessage message = CreateGetFixRunRequest(fixId, options);
            return ClientResult.FromResponse(await Pipeline.ProcessMessageAsync(message, options).ConfigureAwait(false));
        }

        public virtual ClientResult<FixRunEntity> GetFixRun(string fixId, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNullOrEmpty(fixId, nameof(fixId));

            ClientResult result = GetFixRun(fixId, cancellationToken.ToRequestOptions());
            return ClientResult.FromValue((FixRunEntity)result, result.GetRawResponse());
        }

        public virtual async Task<ClientResult<FixRunEntity>> GetFixRunAsync(string fixId, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNullOrEmpty(fixId, nameof(fixId));

            ClientResult result = await GetFixRunAsync(fixId, cancellationToken.ToRequestOptions()).ConfigureAwait(false);
            return ClientResult.FromValue((FixRunEntity)result, result.GetRawResponse());
        }
    }
}
